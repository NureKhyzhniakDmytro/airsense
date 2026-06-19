from __future__ import annotations

import hashlib
import json
import logging
import os
import random
import signal
import threading
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Iterable
from urllib.parse import quote_plus

import numpy as np
import paho.mqtt.client as mqtt
import psycopg
from paho.mqtt.packettypes import PacketTypes
from paho.mqtt.properties import Properties


logging.basicConfig(
    level=os.getenv("LOG_LEVEL", "INFO"),
    format="%(asctime)s %(levelname)s %(message)s",
)
logger = logging.getLogger("device-telemetry-simulator")


SCENARIOS = (
    "auto",
    "empty_room",
    "normal_usage",
    "crowded_room",
    "ventilation_failure",
    "night_mode",
    "critical_co2_event",
)

PARAMETERS = ("co2", "temperature", "humidity", "occupancy")


@dataclass
class RoomState:
    room_id: int
    room_name: str
    sensor_serial: str
    device_id: int
    scenario: str
    co2: float = 520.0
    temperature: float = 22.0
    humidity: float = 45.0
    occupancy: int = 0
    ventilation_power: float = 20.0
    device_state: str = "auto"


@dataclass
class RoomProfile:
    scenario: str = "auto"
    ventilation_power_override: float | None = None
    occupancy_override: int | None = None


def env_int(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None or raw == "":
        return default
    return int(raw)


def env_float(name: str, default: float) -> float:
    raw = os.getenv(name)
    if raw is None or raw == "":
        return default
    return float(raw)


def database_url() -> str:
    raw = os.getenv("DATABASE_URL") or os.getenv("POSTGRES_DSN")
    if raw:
        return raw

    net_connection = os.getenv("ConnectionStrings__DefaultConnection")
    if net_connection:
        parts = {}
        for item in net_connection.split(";"):
            if not item or "=" not in item:
                continue
            key, value = item.split("=", 1)
            parts[key.strip().lower()] = value.strip()

        user = quote_plus(parts.get("username") or parts.get("user id") or "postgres")
        password = quote_plus(parts.get("password") or "")
        host = parts.get("host", "postgres")
        port = parts.get("port", "5432")
        database = quote_plus(parts.get("database", "airsensedb"))
        return f"postgresql://{user}:{password}@{host}:{port}/{database}"

    return "postgresql://postgres:airsense1234@postgres:5432/airsensedb"


def connect_db() -> psycopg.Connection:
    return psycopg.connect(database_url(), autocommit=True)


def clamp(value: float, lower: float, upper: float) -> float:
    return max(lower, min(upper, value))


def choose_scenario(room_id: int, tick: int, rotation_ticks: int) -> str:
    rotating_scenarios = SCENARIOS[1:]
    index = (tick // max(rotation_ticks, 1) + room_id) % len(rotating_scenarios)
    return rotating_scenarios[index]


def scenario_targets(
    scenario: str,
    hour: int,
    rng: np.random.Generator,
    occupancy_override: int | None = None,
    ventilation_power_override: float | None = None,
) -> tuple[int, float, str]:
    if scenario == "empty_room":
        occupancy, power, device_state = 0, 18.0, "idle"
    elif scenario == "crowded_room":
        occupancy, power, device_state = int(rng.integers(12, 26)), 65.0, "boost"
    elif scenario == "ventilation_failure":
        occupancy, power, device_state = int(rng.integers(8, 18)), 5.0, "fault"
    elif scenario == "night_mode":
        occupancy, power, device_state = int(rng.integers(0, 3)), 12.0, "night"
    elif scenario == "critical_co2_event":
        occupancy, power, device_state = int(rng.integers(24, 40)), 45.0 if hour % 2 else 25.0, "boost"
    else:
        occupancy, power, device_state = int(rng.integers(2, 8)), 35.0, "auto"

    if occupancy_override is not None:
        occupancy = int(clamp(float(occupancy_override), 0, 100))
    if ventilation_power_override is not None:
        power = clamp(float(ventilation_power_override), 0, 100)

    return occupancy, power, device_state


def update_state(
    state: RoomState,
    interval_seconds: float,
    rng: np.random.Generator,
    profile: RoomProfile | None = None,
) -> RoomState:
    now = datetime.now(timezone.utc)
    scenario = state.scenario
    if profile and profile.scenario in SCENARIOS and profile.scenario != "auto":
        scenario = profile.scenario

    target_occupancy, target_power, device_state = scenario_targets(
        scenario,
        now.hour,
        rng,
        profile.occupancy_override if profile else None,
        profile.ventilation_power_override if profile else None,
    )

    occupancy_delta = target_occupancy - state.occupancy
    if occupancy_delta != 0:
        state.occupancy += int(np.sign(occupancy_delta) * min(abs(occupancy_delta), max(1, abs(occupancy_delta) // 3)))

    state.ventilation_power += (target_power - state.ventilation_power) * 0.35 + rng.normal(0, 1.2)
    if scenario == "ventilation_failure":
        state.ventilation_power = min(state.ventilation_power, 10.0)

    minutes = interval_seconds / 60.0
    co2_generation = state.occupancy * 18.0 * minutes
    co2_removal = state.ventilation_power * 7.5 * minutes
    outdoor_recovery = (state.co2 - 420.0) * 0.025 * minutes
    critical_boost = 18.0 * minutes if scenario == "critical_co2_event" else 0.0

    state.co2 += co2_generation - co2_removal - outdoor_recovery + critical_boost + rng.normal(0, 7.0)
    state.co2 = clamp(state.co2, 410.0, 3200.0)

    heat_gain = state.occupancy * 0.018 * minutes
    ventilation_cooling = state.ventilation_power * 0.006 * minutes
    night_cooling = 0.025 * minutes if scenario == "night_mode" else 0.0
    state.temperature += heat_gain - ventilation_cooling - night_cooling + rng.normal(0, 0.035)
    state.temperature = clamp(state.temperature, 17.0, 32.0)

    humidity_gain = state.occupancy * 0.03 * minutes
    ventilation_drying = state.ventilation_power * 0.018 * minutes
    state.humidity += humidity_gain - ventilation_drying + rng.normal(0, 0.12)
    state.humidity = clamp(state.humidity, 25.0, 85.0)

    state.ventilation_power = clamp(state.ventilation_power, 0.0, 100.0)
    state.device_state = device_state
    return state


def ensure_parameter(cur, name: str, unit: str, min_value: float, max_value: float) -> int:
    cur.execute("SELECT id FROM parameters WHERE name = %s", (name,))
    row = cur.fetchone()
    if row:
        return row[0]

    cur.execute(
        "INSERT INTO parameters(name, unit, min_value, max_value) VALUES (%s, %s, %s, %s) RETURNING id",
        (name, unit, min_value, max_value),
    )
    return cur.fetchone()[0]


def ensure_sensor_type(cur, type_name: str, parameter_ids: Iterable[int]) -> int:
    cur.execute("SELECT id FROM sensor_types WHERE name = %s", (type_name,))
    row = cur.fetchone()
    if row:
        type_id = row[0]
    else:
        cur.execute("INSERT INTO sensor_types(name) VALUES (%s) RETURNING id", (type_name,))
        type_id = cur.fetchone()[0]

    for parameter_id in parameter_ids:
        cur.execute(
            """
            INSERT INTO sensor_type_parameters(type_id, parameter_id)
            SELECT %s, %s
            WHERE NOT EXISTS (
                SELECT 1 FROM sensor_type_parameters
                WHERE type_id = %s AND parameter_id = %s
            )
            """,
            (type_id, parameter_id, type_id, parameter_id),
        )
    return type_id


def ensure_demo_control_schema(cur) -> None:
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS demo_room_profiles (
            room_id int PRIMARY KEY REFERENCES rooms(id) ON DELETE CASCADE,
            scenario varchar(64) NOT NULL DEFAULT 'auto',
            ventilation_power_override real,
            occupancy_override int,
            updated_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        )
        """
    )


def ensure_environment(cur, name: str, icon: str) -> int:
    cur.execute("SELECT id FROM environments WHERE name = %s ORDER BY id LIMIT 1", (name,))
    row = cur.fetchone()
    if row:
        env_id = row[0]
    else:
        cur.execute("INSERT INTO environments(name, icon) VALUES (%s, %s) RETURNING id", (name, icon))
        env_id = cur.fetchone()[0]

    cur.execute(
        """
        INSERT INTO environment_members(member_id, environment_id, role)
        SELECT id, %s, 'owner'
        FROM users
        ON CONFLICT (member_id, environment_id) DO NOTHING
        """,
        (env_id,),
    )
    return env_id


def ensure_room(cur, env_id: int, room_name: str, icon: str) -> int:
    cur.execute(
        "SELECT id FROM rooms WHERE environment_id = %s AND name = %s ORDER BY id LIMIT 1",
        (env_id, room_name),
    )
    row = cur.fetchone()
    if row:
        return row[0]

    cur.execute(
        "INSERT INTO rooms(name, environment_id, icon) VALUES (%s, %s, %s) RETURNING id",
        (room_name, env_id, icon),
    )
    return cur.fetchone()[0]


def md5(value: str) -> str:
    return hashlib.md5(value.encode("utf-8")).hexdigest()


def ensure_sensor(cur, serial: str, type_id: int, room_id: int) -> None:
    cur.execute("SELECT id FROM sensors WHERE serial_number = %s ORDER BY id LIMIT 1", (serial,))
    row = cur.fetchone()
    if row:
        cur.execute("UPDATE sensors SET type_id = %s, room_id = %s WHERE id = %s", (type_id, room_id, row[0]))
        return

    cur.execute(
        "INSERT INTO sensors(serial_number, type_id, room_id, secret) VALUES (%s, %s, %s, %s)",
        (serial, type_id, room_id, md5(serial + serial)),
    )


def ensure_device(cur, serial: str, room_id: int) -> int:
    cur.execute("SELECT id FROM devices WHERE serial_number = %s ORDER BY id LIMIT 1", (serial,))
    row = cur.fetchone()
    if row:
        cur.execute("UPDATE devices SET room_id = %s WHERE id = %s", (room_id, row[0]))
        return row[0]

    cur.execute(
        "INSERT INTO devices(serial_number, room_id, secret) VALUES (%s, %s, %s) RETURNING id",
        (serial, room_id, md5(serial + serial)),
    )
    return cur.fetchone()[0]


def ensure_co2_curve(cur, room_id: int, co2_parameter_id: int) -> None:
    curve = {
        "CriticalValue": 1400,
        "Points": [
            {"Value": 400, "FanSpeed": 0},
            {"Value": 900, "FanSpeed": 35},
            {"Value": 1400, "FanSpeed": 75},
            {"Value": 2000, "FanSpeed": 100},
        ],
    }
    cur.execute(
        """
        INSERT INTO settings(room_id, parameter_id, curve)
        VALUES (%s, %s, %s::json)
        ON CONFLICT (room_id, parameter_id) DO NOTHING
        """,
        (room_id, co2_parameter_id, json.dumps(curve)),
    )


def ensure_room_profile(cur, room_id: int) -> None:
    cur.execute(
        """
        INSERT INTO demo_room_profiles(room_id, scenario)
        VALUES (%s, 'auto')
        ON CONFLICT (room_id) DO NOTHING
        """,
        (room_id,),
    )


def bootstrap_demo_topology() -> list[RoomState]:
    room_count = env_int("ROOM_COUNT", 4)
    env_name = os.getenv("DEMO_ENVIRONMENT_NAME", "AirSense Demo Environment")
    room_prefix = os.getenv("DEMO_ROOM_PREFIX", "Demo Room")

    with connect_db() as conn:
        with conn.cursor() as cur:
            ensure_demo_control_schema(cur)
            parameter_ids = {
                "temperature": ensure_parameter(cur, "temperature", "°C", -50, 50),
                "humidity": ensure_parameter(cur, "humidity", "%", 0, 100),
                "co2": ensure_parameter(cur, "co2", "ppm", 300, 5000),
                "occupancy": ensure_parameter(cur, "occupancy", "people", 0, 100),
            }
            type_id = ensure_sensor_type(cur, "Microclimate Sensor", parameter_ids.values())
            env_id = ensure_environment(cur, env_name, "factory")

            states: list[RoomState] = []
            for index in range(1, room_count + 1):
                room_name = f"{room_prefix} {index}"
                room_id = ensure_room(cur, env_id, room_name, "factory" if index % 2 else "home")
                sensor_serial = f"demo-room-{room_id}-microclimate"
                device_serial = f"demo-room-{room_id}-ventilation"
                ensure_sensor(cur, sensor_serial, type_id, room_id)
                device_id = ensure_device(cur, device_serial, room_id)
                ensure_co2_curve(cur, room_id, parameter_ids["co2"])
                ensure_room_profile(cur, room_id)

                states.append(
                    RoomState(
                        room_id=room_id,
                        room_name=room_name,
                        sensor_serial=sensor_serial,
                        device_id=device_id,
                        scenario=SCENARIOS[index % len(SCENARIOS)],
                        co2=520.0 + index * 20.0,
                        temperature=21.5 + (index % 3),
                        humidity=42.0 + index,
                    )
                )

            logger.info("Prepared demo topology with %s rooms in environment %s", len(states), env_name)
            return states


def connect_client() -> mqtt.Client:
    host = os.getenv("MQTT_HOST", "emqx")
    port = env_int("MQTT_PORT", 1883)
    username = os.getenv("MQTT_USERNAME", "api")
    password = os.getenv("MQTT_PASSWORD", "")
    client_id = os.getenv("MQTT_CLIENT_ID", f"api-device-telemetry-{random.randint(1000, 9999)}")
    connected = threading.Event()

    client = mqtt.Client(
        mqtt.CallbackAPIVersion.VERSION2,
        client_id=client_id,
        protocol=mqtt.MQTTv5,
    )
    if username:
        client.username_pw_set(username, password)

    def on_connect(_client, _userdata, _flags, reason_code, _properties) -> None:
        if getattr(reason_code, "is_failure", False):
            logger.warning("MQTT connection refused with reason code %s", reason_code)
            return

        connected.set()
        logger.info("Connected to MQTT broker %s:%s as %s", host, port, username)

    def on_disconnect(_client, _userdata, _flags, reason_code, _properties) -> None:
        connected.clear()
        logger.warning("MQTT disconnected with reason code %s", reason_code)

    client.on_connect = on_connect
    client.on_disconnect = on_disconnect
    client.connect(host, port, keepalive=30)
    client.loop_start()

    if not connected.wait(timeout=10):
        raise RuntimeError(f"MQTT connection to {host}:{port} was not established")

    return client


def telemetry_values(state: RoomState) -> dict[str, float]:
    return {
        "co2": round(state.co2, 2),
        "temperature": round(state.temperature, 2),
        "humidity": round(state.humidity, 2),
        "occupancy": float(state.occupancy),
    }


def publish_sensor_value(client: mqtt.Client, serial: str, parameter: str, value: float, sent_at: int) -> None:
    properties = Properties(PacketTypes.PUBLISH)
    properties.UserProperty = [("serial-number", serial)]
    payload = json.dumps({"value": value, "sent_at": sent_at})
    topic = f"sensor/{parameter}"
    result = client.publish(topic, payload, qos=1, properties=properties)
    if result.rc != mqtt.MQTT_ERR_SUCCESS:
        logger.warning("Failed to publish telemetry to %s from %s: %s", topic, serial, result.rc)


def persist_device_state(states: Iterable[RoomState]) -> None:
    with connect_db() as conn:
        with conn.cursor() as cur:
            for state in states:
                cur.execute(
                    """
                    INSERT INTO device_data(device_id, value, applied, applied_at)
                    VALUES (%s, %s, TRUE, CURRENT_TIMESTAMP)
                    """,
                    (state.device_id, round(state.ventilation_power, 2)),
                )


def read_room_profiles() -> dict[int, RoomProfile]:
    try:
        with connect_db() as conn:
            with conn.cursor() as cur:
                ensure_demo_control_schema(cur)
                cur.execute(
                    """
                    SELECT room_id, scenario, ventilation_power_override, occupancy_override
                    FROM demo_room_profiles
                    """
                )
                profiles: dict[int, RoomProfile] = {}
                for room_id, scenario, ventilation_power_override, occupancy_override in cur.fetchall():
                    profiles[int(room_id)] = RoomProfile(
                        scenario=scenario if scenario in SCENARIOS else "auto",
                        ventilation_power_override=(
                            None if ventilation_power_override is None else float(ventilation_power_override)
                        ),
                        occupancy_override=None if occupancy_override is None else int(occupancy_override),
                    )
                return profiles
    except Exception:
        logger.exception("Failed to read demo room profiles")
        return {}


def publish_loop(client: mqtt.Client, states: Iterable[RoomState]) -> None:
    interval_seconds = env_float("PUBLISH_INTERVAL_SECONDS", 10.0)
    rotation_seconds = env_float("SCENARIO_ROTATION_SECONDS", 300.0)
    rotation_ticks = max(1, int(rotation_seconds / interval_seconds))
    seed = env_int("SEED", 42)
    rng = np.random.default_rng(seed)
    stop = False

    def handle_stop(_signum, _frame) -> None:
        nonlocal stop
        stop = True

    signal.signal(signal.SIGTERM, handle_stop)
    signal.signal(signal.SIGINT, handle_stop)

    state_list = list(states)
    profiles: dict[int, RoomProfile] = {}
    profile_refresh_ticks = max(1, int(env_float("PROFILE_REFRESH_SECONDS", 15.0) / interval_seconds))
    tick = 0
    while not stop:
        if tick % profile_refresh_ticks == 0:
            profiles = read_room_profiles()

        for state in state_list:
            profile = profiles.get(state.room_id, RoomProfile())
            if profile.scenario == "auto":
                state.scenario = choose_scenario(state.room_id, tick, rotation_ticks)
            else:
                state.scenario = profile.scenario

            update_state(state, interval_seconds, rng, profile)

            if not client.is_connected():
                logger.warning("MQTT client is not connected; skipping telemetry publish")
                continue

            base_sent_at = int(time.time()) - len(PARAMETERS)
            for offset, (parameter, value) in enumerate(telemetry_values(state).items()):
                publish_sensor_value(client, state.sensor_serial, parameter, value, base_sent_at + offset)

        persist_device_state(state_list)
        tick += 1
        time.sleep(interval_seconds)


def main() -> None:
    states = bootstrap_demo_topology()
    client = connect_client()
    try:
        publish_loop(client, states)
    finally:
        client.loop_stop()
        client.disconnect()


if __name__ == "__main__":
    main()
