from __future__ import annotations

import hashlib
import json
import logging
import math
import os
import random
import signal
import threading
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Iterable
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

PARAMETERS = ("co2", "temperature", "humidity", "pressure")
DEFAULT_DEMO_ENVIRONMENT_ICON = "industrial"
DEFAULT_DEMO_ROOM_ICONS = ("production", "office")
DEFAULT_DEMO_OWNER_EMAIL = "khijnyak.dima@gmail.com"
DEFAULT_DEMO_SENSOR_COUNT = 3
DEFAULT_DEMO_DEVICE_COUNT = 2


@dataclass(frozen=True)
class SensorTarget:
    sensor_id: int
    serial: str
    parameters: tuple[str, ...]
    x: float
    y: float


@dataclass(frozen=True)
class VentTarget:
    device_id: int
    airflow_role: str
    x: float
    y: float
    rotation: float


@dataclass(frozen=True)
class HeatSource:
    x: float
    y: float
    heat_load_kw: float


@dataclass(frozen=True)
class LayoutFeatures:
    width: float
    height: float
    sensor_points_by_id: dict[int, tuple[float, float]]
    sensor_points_by_serial: dict[str, tuple[float, float]]
    vent_targets_by_device_id: dict[int, VentTarget]
    heat_sources: tuple[HeatSource, ...]


@dataclass
class RoomState:
    room_id: int
    room_name: str
    sensor_targets: tuple[SensorTarget, ...]
    device_ids: tuple[int, ...]
    scenario: str
    vent_targets: tuple[VentTarget, ...] = ()
    heat_sources: tuple[HeatSource, ...] = ()
    width: float = 10.0
    height: float = 6.0
    co2: float = 520.0
    temperature: float = 22.0
    humidity: float = 45.0
    pressure: float = 1013.0
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


def env_bool(name: str, default: bool) -> bool:
    raw = os.getenv(name)
    if raw is None or raw == "":
        return default
    return raw.strip().lower() in {"1", "true", "yes", "on"}


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


def to_float(value: Any, default: float) -> float:
    try:
        if value is None or value == "":
            return default
        return float(value)
    except (TypeError, ValueError):
        return default


def to_int(value: Any) -> int | None:
    try:
        if value is None or value == "":
            return None
        return int(value)
    except (TypeError, ValueError):
        return None


def parse_layout(layout: Any) -> dict[str, Any]:
    if isinstance(layout, dict):
        return layout
    if isinstance(layout, str) and layout.strip():
        try:
            parsed = json.loads(layout)
        except json.JSONDecodeError:
            return {}
        return parsed if isinstance(parsed, dict) else {}
    return {}


def layout_items(layout: dict[str, Any]) -> list[dict[str, Any]]:
    items = layout.get("items")
    if not isinstance(items, list):
        return []
    return [item for item in items if isinstance(item, dict)]


def item_center(item: dict[str, Any]) -> tuple[float, float]:
    x = to_float(item.get("x"), 0.0)
    y = to_float(item.get("y"), 0.0)
    width = to_float(item.get("width"), 0.0)
    height = to_float(item.get("height"), 0.0)
    return x + width / 2.0, y + height / 2.0


def gaussian(distance: float, sigma: float) -> float:
    sigma = max(sigma, 0.001)
    return math.exp(-((distance / sigma) ** 2))


def stable_offset(key: str, amplitude: float) -> float:
    digest = hashlib.md5(key.encode("utf-8")).hexdigest()
    raw = int(digest[:8], 16) / 0xFFFFFFFF
    return (raw * 2.0 - 1.0) * amplitude


def airflow_role_from_item(item: dict[str, Any], fallback_index: int = 0) -> str:
    role = str(item.get("airflow_role") or "").strip().lower()
    label = str(item.get("label") or "").strip().lower()
    if role in {"supply", "inlet", "intake"} or any(token in label for token in ("supply", "inlet", "intake")):
        return "supply"
    if role in {"exhaust", "extract", "return", "outlet"} or any(
        token in label for token in ("extract", "exhaust", "return", "outlet")
    ):
        return "exhaust"
    return "exhaust" if fallback_index % 2 == 1 else "supply"


def airflow_axis(rotation: float) -> tuple[float, float]:
    radians = math.radians(rotation)
    return math.cos(radians), math.sin(radians)


def fallback_sensor_point(width: float, height: float, sensor_index: int) -> tuple[float, float]:
    columns = (0.22, 0.5, 0.78)
    row = sensor_index // len(columns)
    column = columns[sensor_index % len(columns)]
    return width * column, height * (0.35 + min(row, 2) * 0.2)


def fallback_vent_target(device_id: int, width: float, height: float, device_index: int) -> VentTarget:
    role = "exhaust" if device_index % 2 == 1 else "supply"
    if role == "supply":
        return VentTarget(device_id=device_id, airflow_role=role, x=0.35, y=height * 0.35, rotation=0.0)
    return VentTarget(device_id=device_id, airflow_role=role, x=max(width - 0.35, 0.0), y=height * 0.65, rotation=180.0)


def heat_load_for_item(item: dict[str, Any]) -> float:
    explicit_load = to_float(item.get("heat_load_kw"), -1.0)
    if explicit_load >= 0:
        return explicit_load

    thermal_load = str(item.get("thermal_load") or "").strip().lower()
    if thermal_load == "high":
        return 14.0
    if thermal_load == "medium":
        return 7.0
    if thermal_load == "low":
        return 2.5
    return 0.0


def extract_layout_features(raw_layout: Any) -> LayoutFeatures:
    layout = parse_layout(raw_layout)
    width = max(to_float(layout.get("width"), 10.0), 1.0)
    height = max(to_float(layout.get("height"), 6.0), 1.0)
    sensor_points_by_id: dict[int, tuple[float, float]] = {}
    sensor_points_by_serial: dict[str, tuple[float, float]] = {}
    vent_targets_by_device_id: dict[int, VentTarget] = {}
    heat_sources: list[HeatSource] = []
    vent_index = 0

    for item in layout_items(layout):
        item_type = str(item.get("type") or "").strip().lower()
        center_x, center_y = item_center(item)

        if item_type == "sensor":
            sensor_id = to_int(item.get("sensor_id"))
            if sensor_id is not None:
                sensor_points_by_id[sensor_id] = (center_x, center_y)
            serial = str(item.get("serial_number") or "").strip()
            if serial:
                sensor_points_by_serial[serial] = (center_x, center_y)
            continue

        if item_type == "vent":
            device_id = to_int(item.get("device_id"))
            if device_id is None:
                continue
            vent_targets_by_device_id[device_id] = VentTarget(
                device_id=device_id,
                airflow_role=airflow_role_from_item(item, vent_index),
                x=center_x,
                y=center_y,
                rotation=to_float(item.get("rotation"), 0.0),
            )
            vent_index += 1
            continue

        if item_type == "equipment":
            heat_load_kw = heat_load_for_item(item)
            if heat_load_kw > 0:
                heat_sources.append(HeatSource(x=center_x, y=center_y, heat_load_kw=heat_load_kw))

    return LayoutFeatures(
        width=width,
        height=height,
        sensor_points_by_id=sensor_points_by_id,
        sensor_points_by_serial=sensor_points_by_serial,
        vent_targets_by_device_id=vent_targets_by_device_id,
        heat_sources=tuple(heat_sources),
    )


def room_shift_activity(now: datetime) -> float:
    hour = now.hour + now.minute / 60.0
    if now.weekday() >= 5:
        return 0.22 if 8.0 <= hour < 16.0 else 0.08
    if 6.0 <= hour < 14.0:
        return 0.92
    if 14.0 <= hour < 22.0:
        return 0.82
    return 0.16


def heat_source_load(state: RoomState, source: HeatSource, now: datetime) -> float:
    activity = room_shift_activity(now)
    cycle = 0.72 + 0.28 * math.sin(now.timestamp() / 780.0 + state.room_id * 0.73 + source.heat_load_kw * 0.11)
    return source.heat_load_kw * clamp(activity * cycle, 0.05, 1.0)


def total_heat_load(state: RoomState, now: datetime) -> float:
    return sum(heat_source_load(state, source, now) for source in state.heat_sources)


def local_heat_at(state: RoomState, x: float, y: float, now: datetime) -> float:
    heat = 0.0
    for source in state.heat_sources:
        distance = math.hypot(x - source.x, y - source.y)
        heat += heat_source_load(state, source, now) * 0.105 * gaussian(distance, 2.8)
    return heat


def ventilation_role_powers(state: RoomState, now: datetime | None = None) -> tuple[float, float]:
    now = now or datetime.now(timezone.utc)
    phase = math.sin(now.timestamp() / 95.0 + state.room_id * 1.37)
    balance = 2.8 * phase
    supply_power = state.ventilation_power + balance
    exhaust_power = state.ventilation_power + 4.0 - balance * 0.55

    if state.scenario == "ventilation_failure":
        supply_power *= 0.72
        exhaust_power *= 0.58
    elif state.scenario == "critical_co2_event":
        supply_power += 3.0
        exhaust_power += 9.0
    elif state.scenario == "night_mode":
        supply_power -= 2.0
        exhaust_power += 1.0

    return clamp(supply_power, 0.0, 100.0), clamp(exhaust_power, 0.0, 100.0)


def vent_influence_at(state: RoomState, x: float, y: float, supply_power: float, exhaust_power: float) -> tuple[float, float]:
    supply = 0.0
    exhaust = 0.0
    for vent in state.vent_targets:
        axis_x, axis_y = airflow_axis(vent.rotation)
        source_x = vent.x + axis_x * 0.35
        source_y = vent.y + axis_y * 0.35
        delta_x = x - source_x
        delta_y = y - source_y
        forward = max(0.0, delta_x * axis_x + delta_y * axis_y)
        lateral = abs(delta_x * -axis_y + delta_y * axis_x)
        distance = math.hypot(delta_x, delta_y)

        if vent.airflow_role == "supply":
            plume = gaussian(forward, 8.0) * gaussian(lateral, 1.9)
            local = gaussian(distance, 1.1)
            supply += (plume + local * 0.18) * (supply_power / 100.0)
        else:
            plume = gaussian(forward, 6.6) * gaussian(lateral, 2.7)
            local = gaussian(distance, 1.35)
            exhaust += (plume + local * 0.24) * (exhaust_power / 100.0)

    return clamp(supply, 0.0, 1.0), clamp(exhaust, 0.0, 1.0)


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

    supply_power, exhaust_power = ventilation_role_powers(state, now)
    exchange_power = (supply_power + exhaust_power) / 2.0
    balance_factor = 1.0 - abs(supply_power - exhaust_power) / 180.0
    active_heat_load_kw = total_heat_load(state, now)
    minutes = interval_seconds / 60.0
    co2_generation = state.occupancy * 6.5 * minutes
    co2_excess = max(state.co2 - 420.0, 0.0)
    co2_removal = co2_excess * (0.055 + exchange_power * 0.0045 * balance_factor) * minutes
    outdoor_recovery = co2_excess * 0.01 * minutes
    critical_boost = 18.0 * minutes if scenario == "critical_co2_event" else 0.0

    state.co2 += co2_generation - co2_removal - outdoor_recovery + critical_boost + rng.normal(0, 7.0)
    state.co2 = clamp(state.co2, 410.0, 3200.0)

    heat_gain = (state.occupancy * 0.018 + active_heat_load_kw * 0.0012) * minutes
    ventilation_cooling = (supply_power * 0.0064 + exhaust_power * 0.0028) * minutes
    night_cooling = 0.025 * minutes if scenario == "night_mode" else 0.0
    state.temperature += heat_gain - ventilation_cooling - night_cooling + rng.normal(0, 0.035)
    state.temperature = clamp(state.temperature, 17.0, 32.0)

    humidity_gain = state.occupancy * 0.03 * minutes
    ventilation_drying = (supply_power * 0.014 + exhaust_power * 0.007) * minutes
    state.humidity += humidity_gain - ventilation_drying + rng.normal(0, 0.12)
    state.humidity = clamp(state.humidity, 25.0, 85.0)

    pressure_target = 1013.0 + math.sin((now.hour + now.minute / 60.0) / 24.0 * math.tau) * 4.5
    state.pressure += (pressure_target - state.pressure) * 0.04 + rng.normal(0, 0.08)
    state.pressure = clamp(state.pressure, 970.0, 1045.0)

    state.ventilation_power = clamp(state.ventilation_power, 0.0, 100.0)
    state.device_state = device_state
    return state


def ensure_parameter(cur, name: str, unit: str, min_value: float, max_value: float) -> int:
    cur.execute("SELECT id FROM parameters WHERE name = %s", (name,))
    row = cur.fetchone()
    if row:
        cur.execute(
            """
            UPDATE parameters
            SET unit = %s,
                min_value = %s,
                max_value = %s
            WHERE id = %s
              AND (
                unit IS DISTINCT FROM %s
                OR min_value IS DISTINCT FROM %s
                OR max_value IS DISTINCT FROM %s
              )
            """,
            (unit, min_value, max_value, row[0], unit, min_value, max_value),
        )
        return row[0]

    cur.execute(
        "INSERT INTO parameters(name, unit, min_value, max_value) VALUES (%s, %s, %s, %s) RETURNING id",
        (name, unit, min_value, max_value),
    )
    return cur.fetchone()[0]


def ensure_sensor_type(cur, type_name: str, parameter_ids: Iterable[int]) -> int:
    requested_parameter_ids = list(dict.fromkeys(parameter_ids))
    cur.execute("SELECT id FROM sensor_types WHERE name = %s", (type_name,))
    row = cur.fetchone()
    if row:
        type_id = row[0]
    else:
        cur.execute("INSERT INTO sensor_types(name) VALUES (%s) RETURNING id", (type_name,))
        type_id = cur.fetchone()[0]

    cur.execute(
        """
        DELETE FROM sensor_type_parameters
        WHERE type_id = %s
          AND NOT (parameter_id = ANY(%s))
        """,
        (type_id, requested_parameter_ids),
    )

    for parameter_id in requested_parameter_ids:
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


def ensure_environment(cur, name: str, icon: str, owner_email: str) -> int:
    cur.execute("SELECT id FROM environments WHERE name = %s ORDER BY id LIMIT 1", (name,))
    row = cur.fetchone()
    if row:
        env_id = row[0]
    else:
        cur.execute("INSERT INTO environments(name, icon) VALUES (%s, %s) RETURNING id", (name, icon))
        env_id = cur.fetchone()[0]

    cur.execute(
        """
        UPDATE environments
        SET icon = %s
        WHERE id = %s
          AND (icon IS NULL OR icon IN ('factory', 'building', ''))
        """,
        (icon, env_id),
    )

    cur.execute(
        """
        WITH demo_owner AS (
            INSERT INTO users(uid, name, email)
            VALUES (%s, %s, %s)
            ON CONFLICT (email) DO UPDATE SET
                name = EXCLUDED.name
            RETURNING id
        )
        INSERT INTO environment_members(member_id, environment_id, role)
        SELECT demo_owner.id, %s, 'owner'
        FROM demo_owner
        ON CONFLICT (member_id, environment_id) DO UPDATE SET
            role = EXCLUDED.role
        """,
        (f"pending:{owner_email}", owner_email, owner_email, env_id),
    )

    cur.execute(
        """
        INSERT INTO environment_members(member_id, environment_id, role)
        SELECT id, %s, 'user'
        FROM users
        ON CONFLICT (member_id, environment_id) DO NOTHING
        """,
        (env_id,),
    )
    return env_id


def ensure_room(cur, env_id: int, room_name: str, icon: str, demo_slot: int | None = None) -> int:
    if demo_slot is not None:
        serials = demo_slot_serials(demo_slot, DEFAULT_DEMO_SENSOR_COUNT, DEFAULT_DEMO_DEVICE_COUNT)
        cur.execute(
            """
            SELECT asset_rooms.room_id
            FROM (
                SELECT s.room_id
                FROM sensors s
                JOIN rooms r ON r.id = s.room_id
                WHERE r.environment_id = %s
                  AND s.serial_number = ANY(%s)
                UNION ALL
                SELECT d.room_id
                FROM devices d
                JOIN rooms r ON r.id = d.room_id
                WHERE r.environment_id = %s
                  AND d.serial_number = ANY(%s)
            ) asset_rooms
            GROUP BY asset_rooms.room_id
            ORDER BY count(*) DESC, asset_rooms.room_id
            LIMIT 1
            """,
            (env_id, list(serials), env_id, list(serials)),
        )
        row = cur.fetchone()
        if row:
            room_id = row[0]
            cur.execute(
                """
                UPDATE rooms
                SET icon = %s
                WHERE id = %s
                  AND icon IN ('factory', 'home')
                """,
                (icon, room_id),
            )
            return room_id

    cur.execute(
        "SELECT id FROM rooms WHERE environment_id = %s AND name = %s ORDER BY id LIMIT 1",
        (env_id, room_name),
    )
    row = cur.fetchone()
    if row:
        room_id = row[0]
        cur.execute(
            """
            UPDATE rooms
            SET icon = %s
            WHERE id = %s
              AND icon IN ('factory', 'home')
            """,
            (icon, room_id),
        )
        return room_id

    cur.execute(
        "INSERT INTO rooms(name, environment_id, icon) VALUES (%s, %s, %s) RETURNING id",
        (room_name, env_id, icon),
    )
    return cur.fetchone()[0]


def md5(value: str) -> str:
    return hashlib.md5(value.encode("utf-8")).hexdigest()


def demo_asset_serial(demo_slot: int, suffix: str, index: int) -> str:
    serial = f"demo-room-{demo_slot}-{suffix}"
    return serial if index <= 1 else f"{serial}-{index}"


def demo_slot_serials(demo_slot: int, sensor_count: int, device_count: int) -> tuple[str, ...]:
    sensors = tuple(demo_asset_serial(demo_slot, "microclimate", index) for index in range(1, sensor_count + 1))
    devices = tuple(demo_asset_serial(demo_slot, "ventilation", index) for index in range(1, device_count + 1))
    return sensors + devices


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


def ensure_room_layout(cur, room_id: int) -> None:
    cur.execute(
        """
        WITH room_scope AS (
            SELECT r.id AS room_id,
                   r.layout,
                   row_number() OVER (ORDER BY r.id) AS demo_index
            FROM rooms r
            WHERE r.environment_id = (SELECT environment_id FROM rooms WHERE id = %s)
        ),
        room_profiles AS (
            SELECT rs.room_id,
                   rs.layout,
                   (mod((rs.demo_index - 1), 4) + 1)::int AS profile_index
            FROM room_scope rs
            WHERE rs.room_id = %s
        ),
        room_data AS (
            SELECT rp.room_id,
                   rp.layout,
                   rp.profile_index,
                   CASE rp.profile_index
                       WHEN 1 THEN 18::numeric
                       WHEN 2 THEN 10::numeric
                       WHEN 3 THEN 12::numeric
                       ELSE 14::numeric
                   END AS width,
                   CASE rp.profile_index
                       WHEN 1 THEN 9::numeric
                       WHEN 2 THEN 7::numeric
                       WHEN 3 THEN 8::numeric
                       ELSE 7::numeric
                   END AS height,
                   'm' AS unit,
                   CASE rp.profile_index
                       WHEN 1 THEN '{"type":"rectangle","points":[{"x":0,"y":0},{"x":18,"y":0},{"x":18,"y":9},{"x":0,"y":9}]}'::jsonb
                       WHEN 2 THEN '{"type":"l_shape","points":[{"x":0,"y":0},{"x":10,"y":0},{"x":10,"y":4.2},{"x":6.6,"y":4.2},{"x":6.6,"y":7},{"x":0,"y":7}]}'::jsonb
                       WHEN 3 THEN '{"type":"t_shape","points":[{"x":3.2,"y":0},{"x":8.8,"y":0},{"x":8.8,"y":2.2},{"x":12,"y":2.2},{"x":12,"y":5.8},{"x":8.8,"y":5.8},{"x":8.8,"y":8},{"x":3.2,"y":8},{"x":3.2,"y":5.8},{"x":0,"y":5.8},{"x":0,"y":2.2},{"x":3.2,"y":2.2}]}'::jsonb
                       ELSE '{"type":"custom","points":[{"x":0,"y":0},{"x":14,"y":0},{"x":14,"y":5.6},{"x":11.8,"y":5.6},{"x":11.8,"y":7},{"x":2.1,"y":7},{"x":2.1,"y":5.9},{"x":0,"y":5.9}]}'::jsonb
                   END AS geometry,
                   CASE
                       WHEN jsonb_typeof(rp.layout -> 'items') = 'array' THEN rp.layout -> 'items'
                       ELSE '[]'::jsonb
                   END AS items
            FROM room_profiles rp
        ),
        current_items AS (
            SELECT rd.room_id,
                   item.value AS item,
                   item.ordinality
            FROM room_data rd
            LEFT JOIN LATERAL jsonb_array_elements(rd.items) WITH ORDINALITY AS item(value, ordinality) ON TRUE
        ),
        preserved_items AS (
            SELECT room_id,
                   COALESCE(
                       jsonb_agg(item ORDER BY ordinality)
                           FILTER (
                               WHERE item IS NOT NULL
                                 AND COALESCE(lower(item ->> 'type'), '') NOT IN ('sensor', 'vent')
                                 AND COALESCE(item ->> 'demo_template_item', 'false') <> 'true'
                                 AND NOT (COALESCE(item ->> 'id', '') = ANY(ARRAY[
                                     'door-main', 'door-service', 'door-lab',
                                     'window-north', 'window-east', 'window-strip',
                                     'operator-zone', 'meeting-zone', 'airlock-zone', 'maintenance-zone',
                                     'machine-press', 'machine-furnace', 'machine-compressor',
                                     'desk-row', 'printer-station', 'storage-shelves',
                                     'lab-bench-a', 'lab-bench-b', 'rack-cold', 'chemical-cabinet',
                                     'rack-east', 'rack-west', 'packing-line', 'obstacle-column'
                                 ]::text[]))
                           ),
                       '[]'::jsonb
                   ) AS items
            FROM current_items
            GROUP BY room_id
        ),
        template_items AS (
            SELECT rd.room_id,
                   COALESCE(jsonb_agg(profile_item.item ORDER BY profile_item.sort_key), '[]'::jsonb) AS items
            FROM room_data rd
            CROSS JOIN LATERAL (
                SELECT *
                FROM (VALUES
                    (1, 10, jsonb_build_object('id', 'door-main', 'type', 'door', 'label', 'Service Door', 'x', -0.54, 'y', 4.94, 'width', 1.4, 'height', 0.32, 'rotation', -90, 'demo_template_item', true)),
                    (1, 20, jsonb_build_object('id', 'window-north', 'type', 'window', 'label', 'High Window', 'x', 5.8, 'y', 0.0, 'width', 3.0, 'height', 0.24, 'rotation', 0, 'demo_template_item', true)),
                    (1, 30, jsonb_build_object('id', 'operator-zone', 'type', 'zone', 'label', 'Operator Shift Zone', 'x', 1.2, 'y', 7.75, 'width', 4.2, 'height', 1.0, 'rotation', 0, 'demo_template_item', true)),
                    (1, 40, jsonb_build_object('id', 'machine-press', 'type', 'equipment', 'label', 'CNC Press #1', 'x', 2.6, 'y', 1.6, 'width', 3.0, 'height', 1.6, 'rotation', 0, 'heat_load_kw', 18.0, 'thermal_load', 'high', 'demo_template_item', true)),
                    (1, 50, jsonb_build_object('id', 'machine-furnace', 'type', 'equipment', 'label', 'Heat Treatment Furnace', 'x', 7.2, 'y', 4.2, 'width', 3.2, 'height', 1.8, 'rotation', 0, 'heat_load_kw', 32.0, 'thermal_load', 'high', 'demo_template_item', true)),
                    (1, 60, jsonb_build_object('id', 'machine-compressor', 'type', 'equipment', 'label', 'Compressor Station', 'x', 12.3, 'y', 5.85, 'width', 2.4, 'height', 1.5, 'rotation', -8, 'heat_load_kw', 14.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                    (2, 10, jsonb_build_object('id', 'door-main', 'type', 'door', 'label', 'Office Entry', 'x', -0.45, 'y', 5.45, 'width', 1.2, 'height', 0.3, 'rotation', -90, 'demo_template_item', true)),
                    (2, 20, jsonb_build_object('id', 'window-east', 'type', 'window', 'label', 'Facade Window', 'x', 8.1, 'y', 0.0, 'width', 1.5, 'height', 0.22, 'rotation', 0, 'demo_template_item', true)),
                    (2, 30, jsonb_build_object('id', 'desk-row', 'type', 'furniture', 'label', 'Desk Row', 'x', 1.0, 'y', 1.0, 'width', 3.3, 'height', 1.25, 'rotation', 0, 'demo_template_item', true)),
                    (2, 40, jsonb_build_object('id', 'meeting-zone', 'type', 'zone', 'label', 'Meeting Zone', 'x', 1.0, 'y', 5.05, 'width', 4.7, 'height', 1.35, 'rotation', 0, 'demo_template_item', true)),
                    (2, 50, jsonb_build_object('id', 'printer-station', 'type', 'equipment', 'label', 'Printer Station', 'x', 7.25, 'y', 1.1, 'width', 1.1, 'height', 0.8, 'rotation', 0, 'heat_load_kw', 2.5, 'thermal_load', 'low', 'demo_template_item', true)),
                    (2, 60, jsonb_build_object('id', 'storage-shelves', 'type', 'obstacle', 'label', 'Storage Shelves', 'x', 5.0, 'y', 2.55, 'width', 1.2, 'height', 1.05, 'rotation', 0, 'demo_template_item', true)),
                    (3, 10, jsonb_build_object('id', 'door-lab', 'type', 'door', 'label', 'Lab Entry', 'x', 5.4, 'y', 7.72, 'width', 1.2, 'height', 0.28, 'rotation', 180, 'demo_template_item', true)),
                    (3, 20, jsonb_build_object('id', 'airlock-zone', 'type', 'zone', 'label', 'Airlock Zone', 'x', 4.6, 'y', 5.9, 'width', 2.8, 'height', 1.15, 'rotation', 0, 'demo_template_item', true)),
                    (3, 30, jsonb_build_object('id', 'lab-bench-a', 'type', 'equipment', 'label', 'Process Bench A', 'x', 1.0, 'y', 3.0, 'width', 2.0, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 5.0, 'thermal_load', 'low', 'demo_template_item', true)),
                    (3, 40, jsonb_build_object('id', 'lab-bench-b', 'type', 'equipment', 'label', 'Process Bench B', 'x', 9.0, 'y', 3.0, 'width', 2.0, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 7.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                    (3, 50, jsonb_build_object('id', 'rack-cold', 'type', 'equipment', 'label', 'Cold Storage Rack', 'x', 4.05, 'y', 0.55, 'width', 3.9, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 3.5, 'thermal_load', 'low', 'demo_template_item', true)),
                    (3, 60, jsonb_build_object('id', 'chemical-cabinet', 'type', 'obstacle', 'label', 'Chemical Cabinet', 'x', 4.25, 'y', 3.45, 'width', 1.2, 'height', 0.9, 'rotation', 0, 'demo_template_item', true)),
                    (4, 10, jsonb_build_object('id', 'door-service', 'type', 'door', 'label', 'Service Door', 'x', -0.48, 'y', 3.18, 'width', 1.25, 'height', 0.3, 'rotation', -90, 'demo_template_item', true)),
                    (4, 20, jsonb_build_object('id', 'window-strip', 'type', 'window', 'label', 'Inspection Window', 'x', 4.2, 'y', 0.0, 'width', 2.2, 'height', 0.22, 'rotation', 0, 'demo_template_item', true)),
                    (4, 30, jsonb_build_object('id', 'rack-east', 'type', 'equipment', 'label', 'Server Rack East', 'x', 9.9, 'y', 0.9, 'width', 1.2, 'height', 3.0, 'rotation', 0, 'heat_load_kw', 9.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                    (4, 40, jsonb_build_object('id', 'rack-west', 'type', 'equipment', 'label', 'Server Rack West', 'x', 3.0, 'y', 0.9, 'width', 1.2, 'height', 3.0, 'rotation', 0, 'heat_load_kw', 8.5, 'thermal_load', 'medium', 'demo_template_item', true)),
                    (4, 50, jsonb_build_object('id', 'packing-line', 'type', 'equipment', 'label', 'Packing Line', 'x', 5.3, 'y', 4.6, 'width', 4.4, 'height', 1.0, 'rotation', 0, 'heat_load_kw', 11.0, 'thermal_load', 'medium', 'demo_template_item', true)),
                    (4, 60, jsonb_build_object('id', 'maintenance-zone', 'type', 'zone', 'label', 'Maintenance Zone', 'x', 1.0, 'y', 4.35, 'width', 3.0, 'height', 1.15, 'rotation', 0, 'demo_template_item', true)),
                    (4, 70, jsonb_build_object('id', 'obstacle-column', 'type', 'obstacle', 'label', 'Structural Column', 'x', 6.65, 'y', 2.4, 'width', 0.55, 'height', 0.55, 'rotation', 0, 'demo_template_item', true))
                ) AS profile_item(profile_index, sort_key, item)
                WHERE profile_item.profile_index = rd.profile_index
            ) profile_item
            GROUP BY rd.room_id
        ),
        ranked_sensors AS (
            SELECT rd.room_id,
                   rd.width,
                   rd.height,
                   rd.profile_index,
                   s.id,
                   s.serial_number,
                   row_number() OVER (PARTITION BY rd.room_id ORDER BY s.id) AS rn
            FROM room_data rd
            JOIN sensors s ON s.room_id = rd.room_id
        ),
        ranked_devices AS (
            SELECT rd.room_id,
                   rd.width,
                   rd.height,
                   rd.profile_index,
                   d.id,
                   d.serial_number,
                   row_number() OVER (PARTITION BY rd.room_id ORDER BY d.id) AS rn
            FROM room_data rd
            JOIN devices d ON d.room_id = rd.room_id
        ),
        sensor_defaults AS (
            SELECT s.*,
                   CASE s.rn
                       WHEN 1 THEN CASE s.profile_index WHEN 1 THEN 'S1 Press Zone' WHEN 2 THEN 'S1 Supply Zone' WHEN 3 THEN 'S1 Left Bench' ELSE 'S1 Rack Intake' END
                       WHEN 2 THEN CASE s.profile_index WHEN 1 THEN 'S2 Furnace Zone' WHEN 2 THEN 'S2 Meeting Zone' WHEN 3 THEN 'S2 Center Cross' ELSE 'S2 Line Center' END
                       WHEN 3 THEN CASE s.profile_index WHEN 1 THEN 'S3 Exhaust Zone' WHEN 2 THEN 'S3 Return Zone' WHEN 3 THEN 'S3 Right Bench' ELSE 'S3 Exhaust Zone' END
                       ELSE 'Sensor #' || s.id
                   END AS default_label,
                   CASE s.profile_index
                       WHEN 1 THEN CASE s.rn WHEN 1 THEN 2.92 WHEN 2 THEN 8.32 WHEN 3 THEN 14.32 ELSE greatest(0::numeric, least(s.width - 0.56, 1.0 + (mod(s.rn - 1, 5)::numeric * 2.4))) END
                       WHEN 2 THEN CASE s.rn WHEN 1 THEN 1.2 WHEN 2 THEN 5.25 WHEN 3 THEN 8.45 ELSE greatest(0::numeric, least(s.width - 0.56, 0.9 + (mod(s.rn - 1, 4)::numeric * 1.6))) END
                       WHEN 3 THEN CASE s.rn WHEN 1 THEN 1.1 WHEN 2 THEN 5.72 WHEN 3 THEN 10.05 ELSE greatest(0::numeric, least(s.width - 0.56, 1.0 + (mod(s.rn - 1, 4)::numeric * 2.1))) END
                       ELSE CASE s.rn WHEN 1 THEN 2.0 WHEN 2 THEN 6.5 WHEN 3 THEN 11.6 ELSE greatest(0::numeric, least(s.width - 0.56, 1.0 + (mod(s.rn - 1, 4)::numeric * 2.4))) END
                   END AS default_x,
                   CASE s.profile_index
                       WHEN 1 THEN CASE s.rn WHEN 1 THEN 4.02 WHEN 2 THEN 6.62 WHEN 3 THEN 4.12 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 5)::numeric * 1.2))) END
                       WHEN 2 THEN CASE s.rn WHEN 1 THEN 2.55 WHEN 2 THEN 5.75 WHEN 3 THEN 2.75 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 4)::numeric * 1.1))) END
                       WHEN 3 THEN CASE s.rn WHEN 1 THEN 3.42 WHEN 2 THEN 1.0 WHEN 3 THEN 3.42 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 4)::numeric * 1.2))) END
                       ELSE CASE s.rn WHEN 1 THEN 1.4 WHEN 2 THEN 4.7 WHEN 3 THEN 2.2 ELSE greatest(0::numeric, least(s.height - 0.56, 0.8 + (((s.rn - 1) / 4)::numeric * 1.1))) END
                   END AS default_y
            FROM ranked_sensors s
        ),
        device_defaults AS (
            SELECT d.*,
                   CASE WHEN mod(d.rn, 2) = 0 THEN 'exhaust' ELSE 'supply' END AS default_airflow_role,
                   CASE d.rn
                       WHEN 1 THEN 'V1 Supply Fan'
                       WHEN 2 THEN 'V2 Extract Fan'
                       ELSE 'Vent #' || d.id
                   END AS default_label,
                   CASE d.profile_index
                       WHEN 1 THEN CASE d.rn WHEN 1 THEN 16.95 WHEN 2 THEN 16.95 WHEN 3 THEN 0.25 WHEN 4 THEN 0.25 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (mod(d.rn - 1, 3)::numeric * 1.0))) END
                       WHEN 2 THEN CASE d.rn WHEN 1 THEN 0.25 WHEN 2 THEN 8.95 WHEN 3 THEN 3.1 WHEN 4 THEN 6.0 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (mod(d.rn - 1, 3)::numeric * 1.0))) END
                       WHEN 3 THEN CASE d.rn WHEN 1 THEN 5.6 WHEN 2 THEN 5.6 WHEN 3 THEN 0.25 WHEN 4 THEN 10.95 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (mod(d.rn - 1, 3)::numeric * 1.0))) END
                       ELSE CASE d.rn WHEN 1 THEN 0.3 WHEN 2 THEN 12.9 WHEN 3 THEN 6.7 WHEN 4 THEN 10.7 ELSE greatest(0::numeric, least(d.width - 0.8, d.width - 1.1 - (mod(d.rn - 1, 3)::numeric * 1.0))) END
                   END AS default_x,
                   CASE d.profile_index
                       WHEN 1 THEN CASE d.rn WHEN 1 THEN 1.25 WHEN 2 THEN 7.7 WHEN 3 THEN 1.0 WHEN 4 THEN 7.2 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                       WHEN 2 THEN CASE d.rn WHEN 1 THEN 1.0 WHEN 2 THEN 3.1 WHEN 3 THEN 6.05 WHEN 4 THEN 0.25 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                       WHEN 3 THEN CASE d.rn WHEN 1 THEN 7.05 WHEN 2 THEN 0.2 WHEN 3 THEN 3.2 WHEN 4 THEN 3.2 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                       ELSE CASE d.rn WHEN 1 THEN 2.6 WHEN 2 THEN 1.2 WHEN 3 THEN 6.1 WHEN 4 THEN 5.05 ELSE greatest(0::numeric, least(d.height - 0.8, 0.7 + (((d.rn - 1) / 3)::numeric * 1.1))) END
                   END AS default_y,
                   CASE d.profile_index
                       WHEN 1 THEN CASE d.rn WHEN 1 THEN 180 WHEN 2 THEN 180 WHEN 3 THEN 0 WHEN 4 THEN 0 ELSE 180 END
                       WHEN 2 THEN CASE d.rn WHEN 1 THEN 0 WHEN 2 THEN 180 WHEN 3 THEN 270 WHEN 4 THEN 90 ELSE 180 END
                       WHEN 3 THEN CASE d.rn WHEN 1 THEN 270 WHEN 2 THEN 90 WHEN 3 THEN 0 WHEN 4 THEN 180 ELSE 180 END
                       ELSE CASE d.rn WHEN 1 THEN 0 WHEN 2 THEN 180 WHEN 3 THEN 270 WHEN 4 THEN 180 ELSE 180 END
                   END AS default_rotation
            FROM ranked_devices d
        ),
        existing_sensor_items AS (
            SELECT s.room_id,
                   10 AS sort_group,
                   ci.ordinality AS sort_key,
                   (ci.item - 'device_id') || jsonb_build_object(
                       'id', 'sensor-' || s.id,
                       'type', 'sensor',
                       'label', s.default_label,
                       'sensor_id', s.id,
                       'serial_number', s.serial_number,
                       'x', round(s.default_x, 2),
                       'y', round(s.default_y, 2),
                       'width', 0.56,
                       'height', 0.56,
                       'rotation', 0
                   ) AS item
            FROM sensor_defaults s
            JOIN LATERAL (
                SELECT item, ordinality
                FROM current_items ci
                WHERE ci.room_id = s.room_id
                  AND lower(ci.item ->> 'type') = 'sensor'
                  AND (ci.item ->> 'sensor_id') ~ '^[0-9]+$'
                  AND (ci.item ->> 'sensor_id')::int = s.id
                ORDER BY ordinality
                LIMIT 1
            ) ci ON TRUE
        ),
        missing_sensor_items AS (
            SELECT s.room_id,
                   10 AS sort_group,
                   s.rn + 10000 AS sort_key,
                   jsonb_build_object(
                       'id', 'sensor-' || s.id,
                       'type', 'sensor',
                       'label', s.default_label,
                       'sensor_id', s.id,
                       'serial_number', s.serial_number,
                       'x', round(s.default_x, 2),
                       'y', round(s.default_y, 2),
                       'width', 0.56,
                       'height', 0.56,
                       'rotation', 0
                   ) AS item
            FROM sensor_defaults s
            WHERE NOT EXISTS (
                SELECT 1
                FROM existing_sensor_items existing
                WHERE existing.room_id = s.room_id
                  AND (existing.item ->> 'sensor_id')::int = s.id
            )
        ),
        existing_device_items AS (
            SELECT d.room_id,
                   20 AS sort_group,
                   ci.ordinality AS sort_key,
                   (ci.item - 'sensor_id') || jsonb_build_object(
                       'id', 'vent-' || d.id,
                       'type', 'vent',
                       'label', d.default_label,
                       'device_id', d.id,
                       'serial_number', d.serial_number,
                       'airflow_role', d.default_airflow_role,
                       'x', round(d.default_x, 2),
                       'y', round(d.default_y, 2),
                       'width', 0.8,
                       'height', 0.8,
                       'rotation', d.default_rotation
                   ) AS item
            FROM device_defaults d
            JOIN LATERAL (
                SELECT item, ordinality
                FROM current_items ci
                WHERE ci.room_id = d.room_id
                  AND lower(ci.item ->> 'type') = 'vent'
                  AND (ci.item ->> 'device_id') ~ '^[0-9]+$'
                  AND (ci.item ->> 'device_id')::int = d.id
                ORDER BY ordinality
                LIMIT 1
            ) ci ON TRUE
        ),
        missing_device_items AS (
            SELECT d.room_id,
                   20 AS sort_group,
                   d.rn + 10000 AS sort_key,
                   jsonb_build_object(
                       'id', 'vent-' || d.id,
                       'type', 'vent',
                       'label', d.default_label,
                       'device_id', d.id,
                       'serial_number', d.serial_number,
                       'airflow_role', d.default_airflow_role,
                       'x', round(d.default_x, 2),
                       'y', round(d.default_y, 2),
                       'width', 0.8,
                       'height', 0.8,
                       'rotation', d.default_rotation
                   ) AS item
            FROM device_defaults d
            WHERE NOT EXISTS (
                SELECT 1
                FROM existing_device_items existing
                WHERE existing.room_id = d.room_id
                  AND (existing.item ->> 'device_id')::int = d.id
            )
        ),
        asset_items AS (
            SELECT room_id, sort_group, sort_key, item FROM existing_sensor_items
            UNION ALL
            SELECT room_id, sort_group, sort_key, item FROM missing_sensor_items
            UNION ALL
            SELECT room_id, sort_group, sort_key, item FROM existing_device_items
            UNION ALL
            SELECT room_id, sort_group, sort_key, item FROM missing_device_items
        ),
        generated_items AS (
            SELECT room_id,
                   COALESCE(jsonb_agg(item ORDER BY sort_group, sort_key), '[]'::jsonb) AS items
            FROM asset_items
            GROUP BY room_id
        ),
        next_layouts AS (
            SELECT rd.room_id,
                   jsonb_build_object(
                       'width', rd.width,
                       'height', rd.height,
                       'unit', rd.unit,
                       'geometry', rd.geometry,
                       'demo_template_version', 'rich-demo-v2',
                       'items', COALESCE(t.items, '[]'::jsonb) || COALESCE(p.items, '[]'::jsonb) || COALESCE(g.items, '[]'::jsonb)
                   ) AS layout
            FROM room_data rd
            LEFT JOIN template_items t ON t.room_id = rd.room_id
            LEFT JOIN preserved_items p ON p.room_id = rd.room_id
            LEFT JOIN generated_items g ON g.room_id = rd.room_id
        )
        UPDATE rooms r
        SET layout = next_layouts.layout
        FROM next_layouts
        WHERE r.id = next_layouts.room_id
          AND r.layout IS DISTINCT FROM next_layouts.layout
        """,
        (room_id, room_id),
    )


def ensure_simulation_catalog(cur) -> tuple[dict[str, int], int]:
    parameter_ids = {
        "temperature": ensure_parameter(cur, "temperature", "°C", -50, 50),
        "humidity": ensure_parameter(cur, "humidity", "%", 0, 100),
        "co2": ensure_parameter(cur, "co2", "ppm", 300, 5000),
        "pressure": ensure_parameter(cur, "pressure", "hPa", 300, 1100),
    }
    type_id = ensure_sensor_type(
        cur,
        "Microclimate Sensor",
        (parameter_ids["temperature"], parameter_ids["humidity"], parameter_ids["co2"]),
    )
    return parameter_ids, type_id


def seed_demo_topology_if_empty(cur, parameter_ids: dict[str, int], sensor_type_id: int) -> None:
    if not env_bool("SEED_DEMO_TOPOLOGY", True):
        return

    cur.execute("SELECT COUNT(*) FROM rooms")
    if cur.fetchone()[0] > 0:
        return

    room_count = env_int("ROOM_COUNT", 4)
    sensor_count = max(DEFAULT_DEMO_SENSOR_COUNT, env_int("DEMO_SENSOR_COUNT", DEFAULT_DEMO_SENSOR_COUNT))
    device_count = max(DEFAULT_DEMO_DEVICE_COUNT, env_int("DEMO_DEVICE_COUNT", DEFAULT_DEMO_DEVICE_COUNT))
    env_name = os.getenv("DEMO_ENVIRONMENT_NAME", "AirSense Demo Environment")
    env_icon = os.getenv("DEMO_ENVIRONMENT_ICON", DEFAULT_DEMO_ENVIRONMENT_ICON)
    owner_email = os.getenv("DEMO_OWNER_EMAIL", DEFAULT_DEMO_OWNER_EMAIL)
    room_icons = tuple(
        icon.strip()
        for icon in os.getenv("DEMO_ROOM_ICONS", ",".join(DEFAULT_DEMO_ROOM_ICONS)).split(",")
        if icon.strip()
    ) or DEFAULT_DEMO_ROOM_ICONS
    room_prefix = os.getenv("DEMO_ROOM_PREFIX", "Demo Room")

    env_id = ensure_environment(cur, env_name, env_icon, owner_email)
    for index in range(1, room_count + 1):
        room_name = f"{room_prefix} {index}"
        room_id = ensure_room(cur, env_id, room_name, room_icons[(index - 1) % len(room_icons)], demo_slot=index)
        sensor_serials = tuple(
            demo_asset_serial(index, "microclimate", asset_index)
            for asset_index in range(1, sensor_count + 1)
        )
        device_serials = tuple(
            demo_asset_serial(index, "ventilation", asset_index)
            for asset_index in range(1, device_count + 1)
        )

        for sensor_serial in sensor_serials:
            ensure_sensor(cur, sensor_serial, sensor_type_id, room_id)
        for device_serial in device_serials:
            ensure_device(cur, device_serial, room_id)

        ensure_co2_curve(cur, room_id, parameter_ids["co2"])
        ensure_room_profile(cur, room_id)
        ensure_room_layout(cur, room_id)

    logger.info("Seeded %s demo rooms because the database had no rooms", room_count)


def load_room_states(cur) -> list[RoomState]:
    cur.execute("SELECT id, name, layout FROM rooms ORDER BY id")
    rooms = cur.fetchall()
    layout_features_by_room = {
        int(room_id): extract_layout_features(layout)
        for room_id, _room_name, layout in rooms
    }

    cur.execute(
        """
        SELECT
            s.room_id,
            s.id,
            s.serial_number,
            COALESCE(
                array_agg(p.name ORDER BY p.name) FILTER (WHERE p.name IS NOT NULL),
                ARRAY[]::text[]
            ) AS parameters
        FROM sensors s
        JOIN sensor_types st ON st.id = s.type_id
        LEFT JOIN sensor_type_parameters stp ON stp.type_id = st.id
        LEFT JOIN parameters p ON p.id = stp.parameter_id
        WHERE s.room_id IS NOT NULL
        GROUP BY s.id, s.room_id, s.serial_number
        ORDER BY s.room_id, s.id
        """
    )
    sensor_rows_by_room: dict[int, list[tuple[int, str, tuple[str, ...]]]] = {}
    for room_id, sensor_id, serial, parameters in cur.fetchall():
        enabled_parameters = tuple(parameter for parameter in parameters if parameter in PARAMETERS)
        if not enabled_parameters:
            continue
        sensor_rows_by_room.setdefault(int(room_id), []).append((int(sensor_id), str(serial), enabled_parameters))

    cur.execute(
        """
        SELECT room_id, id
        FROM devices
        WHERE room_id IS NOT NULL
        ORDER BY room_id, id
        """
    )
    devices_by_room: dict[int, list[int]] = {}
    for room_id, device_id in cur.fetchall():
        devices_by_room.setdefault(int(room_id), []).append(int(device_id))

    states: list[RoomState] = []
    for index, (room_id, room_name, _layout) in enumerate(rooms, start=1):
        room_id = int(room_id)
        layout_features = layout_features_by_room.get(room_id, extract_layout_features({}))
        sensor_targets: list[SensorTarget] = []
        for sensor_index, (sensor_id, serial, enabled_parameters) in enumerate(sensor_rows_by_room.get(room_id, ())):
            x, y = (
                layout_features.sensor_points_by_id.get(sensor_id)
                or layout_features.sensor_points_by_serial.get(serial)
                or fallback_sensor_point(layout_features.width, layout_features.height, sensor_index)
            )
            sensor_targets.append(
                SensorTarget(
                    sensor_id=sensor_id,
                    serial=serial,
                    parameters=enabled_parameters,
                    x=x,
                    y=y,
                )
            )

        device_ids = tuple(devices_by_room.get(room_id, ()))
        vent_targets = tuple(
            layout_features.vent_targets_by_device_id.get(device_id)
            or fallback_vent_target(device_id, layout_features.width, layout_features.height, device_index)
            for device_index, device_id in enumerate(device_ids)
        )
        states.append(
            RoomState(
                room_id=room_id,
                room_name=str(room_name),
                sensor_targets=tuple(sensor_targets),
                device_ids=device_ids,
                scenario=SCENARIOS[room_id % len(SCENARIOS)],
                vent_targets=vent_targets,
                heat_sources=layout_features.heat_sources,
                width=layout_features.width,
                height=layout_features.height,
                co2=500.0 + (room_id % 9) * 24.0,
                temperature=21.0 + (room_id % 5) * 0.45,
                humidity=40.0 + (room_id % 7) * 1.2,
                pressure=1010.0 + (index % 6) * 0.8,
            )
        )

    logger.info(
        "Prepared simulation topology with %s rooms, %s sensors, %s devices",
        len(states),
        sum(len(state.sensor_targets) for state in states),
        sum(len(state.device_ids) for state in states),
    )
    return states


def bootstrap_demo_topology() -> list[RoomState]:
    with connect_db() as conn:
        with conn.cursor() as cur:
            ensure_demo_control_schema(cur)
            parameter_ids, sensor_type_id = ensure_simulation_catalog(cur)
            seed_demo_topology_if_empty(cur, parameter_ids, sensor_type_id)
            return load_room_states(cur)


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


def telemetry_values(
    state: RoomState,
    target: SensorTarget,
    sensor_index: int,
    rng: np.random.Generator,
    supply_power: float,
    exhaust_power: float,
) -> dict[str, float]:
    now = datetime.now(timezone.utc)
    supply_airflow, exhaust_airflow = vent_influence_at(state, target.x, target.y, supply_power, exhaust_power)
    local_heat = local_heat_at(state, target.x, target.y, now)
    sensor_phase = math.sin(now.timestamp() / 125.0 + state.room_id * 0.47 + sensor_index * 1.18)
    co2_offset = stable_offset(f"{target.serial}:co2", 9.0) + state.occupancy * (1.1 + sensor_phase * 0.35)
    temperature_offset = stable_offset(f"{target.serial}:temperature", 0.18) + local_heat
    humidity_offset = stable_offset(f"{target.serial}:humidity", 0.38) + state.occupancy * 0.045 - local_heat * 0.12
    pressure_offset = stable_offset(f"{target.serial}:pressure", 0.09) + (exhaust_power - supply_power) * 0.006
    values: dict[str, float] = {}

    for parameter in target.parameters:
        if parameter == "co2":
            ventilation_offset = supply_airflow * (32.0 + supply_power * 0.25) + exhaust_airflow * (24.0 + exhaust_power * 0.18)
            values[parameter] = round(
                clamp(state.co2 + co2_offset - ventilation_offset + rng.normal(0, 8.5), 410.0, 3200.0),
                2,
            )
        elif parameter == "temperature":
            ventilation_offset = supply_airflow * (0.55 + supply_power * 0.008) + exhaust_airflow * (0.22 + exhaust_power * 0.0035)
            values[parameter] = round(
                clamp(state.temperature + temperature_offset - ventilation_offset + rng.normal(0, 0.085), 17.0, 35.0),
                2,
            )
        elif parameter == "humidity":
            ventilation_offset = supply_airflow * (1.05 + supply_power * 0.012) + exhaust_airflow * (0.5 + exhaust_power * 0.006)
            values[parameter] = round(
                clamp(state.humidity + humidity_offset - ventilation_offset + rng.normal(0, 0.28), 20.0, 90.0),
                2,
            )
        elif parameter == "pressure":
            values[parameter] = round(clamp(state.pressure + pressure_offset + rng.normal(0, 0.045), 970.0, 1045.0), 2)

    return values


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
                now = datetime.now(timezone.utc)
                supply_power, exhaust_power = ventilation_role_powers(state, now)
                vent_targets = state.vent_targets or tuple(
                    fallback_vent_target(device_id, state.width, state.height, index)
                    for index, device_id in enumerate(state.device_ids)
                )
                for index, vent in enumerate(vent_targets):
                    base_value = exhaust_power if vent.airflow_role == "exhaust" else supply_power
                    pulse = math.sin(now.timestamp() / 37.0 + state.room_id * 0.61 + index * 1.17) * 1.35
                    value = clamp(base_value + pulse, 0.0, 100.0)
                    cur.execute(
                        """
                        INSERT INTO device_data(device_id, value, applied, applied_at)
                        VALUES (%s, %s, TRUE, CURRENT_TIMESTAMP)
                        """,
                        (vent.device_id, round(value, 2)),
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


def refresh_room_states(existing_states: Iterable[RoomState]) -> list[RoomState]:
    existing_by_room = {state.room_id: state for state in existing_states}
    try:
        with connect_db() as conn:
            with conn.cursor() as cur:
                next_states = load_room_states(cur)
    except Exception:
        logger.exception("Failed to refresh simulation topology")
        return list(existing_by_room.values())

    for state in next_states:
        existing = existing_by_room.get(state.room_id)
        if existing is None:
            continue

        state.scenario = existing.scenario
        state.co2 = existing.co2
        state.temperature = existing.temperature
        state.humidity = existing.humidity
        state.pressure = existing.pressure
        state.occupancy = existing.occupancy
        state.ventilation_power = existing.ventilation_power
        state.device_state = existing.device_state

    return next_states


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
    topology_refresh_ticks = max(1, int(env_float("TOPOLOGY_REFRESH_SECONDS", 30.0) / interval_seconds))
    tick = 0
    while not stop:
        if tick > 0 and tick % topology_refresh_ticks == 0:
            state_list = refresh_room_states(state_list)

        if tick % profile_refresh_ticks == 0:
            profiles = read_room_profiles()

        for state in state_list:
            profile = profiles.get(state.room_id, RoomProfile())
            if profile.scenario == "auto":
                state.scenario = choose_scenario(state.room_id, tick, rotation_ticks)
            else:
                state.scenario = profile.scenario

            update_state(state, interval_seconds, rng, profile)
            supply_power, exhaust_power = ventilation_role_powers(state)

            if not client.is_connected():
                logger.warning("MQTT client is not connected; skipping telemetry publish")
                continue

            published_values = sum(len(target.parameters) for target in state.sensor_targets)
            base_sent_at = int(time.time()) - max(published_values, 1)
            sent_at_offset = 0
            for sensor_index, target in enumerate(state.sensor_targets):
                for parameter, value in telemetry_values(
                    state,
                    target,
                    sensor_index,
                    rng,
                    supply_power,
                    exhaust_power,
                ).items():
                    publish_sensor_value(
                        client,
                        target.serial,
                        parameter,
                        value,
                        base_sent_at + sent_at_offset,
                    )
                    sent_at_offset += 1

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
