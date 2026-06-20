#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from urllib.parse import quote_plus

import psycopg


DEMO_ENVIRONMENT_NAME = "AirSense Demo Environment"
ROOM_NAME = "Industrial Production Bay"
PARAMETERS = {
    "temperature": ("°C", -50, 50),
    "humidity": ("%", 0, 100),
    "co2": ("ppm", 300, 5000),
}


@dataclass(frozen=True)
class Point:
    x: float
    y: float


@dataclass(frozen=True)
class Machine:
    key: str
    label: str
    center: Point
    heat_load_kw: float
    width: float
    height: float
    rotation: float
    profile: str


@dataclass(frozen=True)
class SensorPlacement:
    serial_suffix: str
    label: str
    point: Point


@dataclass(frozen=True)
class VentPlacement:
    serial_suffix: str
    label: str
    x: float
    y: float
    rotation: float


MACHINES = [
    Machine("press", "CNC Press #1", Point(4.1, 2.4), 18.0, 3.0, 1.6, 0, "day_evening"),
    Machine("furnace", "Heat Treatment Furnace", Point(8.8, 5.1), 32.0, 3.2, 1.8, 0, "batch_heat"),
    Machine("compressor", "Compressor Station", Point(13.5, 6.6), 14.0, 2.4, 1.5, -8, "continuous"),
]
SENSORS = [
    SensorPlacement("", "S1 Press Zone", Point(3.2, 4.3)),
    SensorPlacement("-2", "S2 Furnace Zone", Point(8.6, 6.9)),
    SensorPlacement("-3", "S3 Exhaust Zone", Point(14.6, 4.4)),
]
VENTS = [
    VentPlacement("", "V1 Supply Fan", 16.95, 1.25, 180),
    VentPlacement("-2", "V2 Extract Fan", 16.95, 7.7, 180),
]


def database_url() -> str:
    raw = os.getenv("DATABASE_URL") or os.getenv("POSTGRES_DSN")
    if raw:
        return raw

    net_connection = os.getenv("ConnectionStrings__DefaultConnection")
    if net_connection:
        parts: dict[str, str] = {}
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


def md5(value: str) -> str:
    return hashlib.md5(value.encode("utf-8")).hexdigest()


def clamp(value: float, lower: float, upper: float) -> float:
    return max(lower, min(upper, value))


def ensure_parameter(cur, name: str, unit: str, min_value: float, max_value: float) -> int:
    row = cur.execute("SELECT id FROM parameters WHERE name = %s", (name,)).fetchone()
    if row:
        cur.execute(
            """
            UPDATE parameters
            SET unit = %s, min_value = %s, max_value = %s
            WHERE id = %s
            """,
            (unit, min_value, max_value, row[0]),
        )
        return int(row[0])

    return int(cur.execute(
        "INSERT INTO parameters(name, unit, min_value, max_value) VALUES (%s, %s, %s, %s) RETURNING id",
        (name, unit, min_value, max_value),
    ).fetchone()[0])


def ensure_sensor_type(cur, parameter_ids: list[int]) -> int:
    row = cur.execute("SELECT id FROM sensor_types WHERE name = %s", ("Microclimate Sensor",)).fetchone()
    if row:
        type_id = int(row[0])
    else:
        type_id = int(cur.execute(
            "INSERT INTO sensor_types(name) VALUES (%s) RETURNING id",
            ("Microclimate Sensor",),
        ).fetchone()[0])

    cur.execute("DELETE FROM sensor_type_parameters WHERE type_id = %s AND NOT (parameter_id = ANY(%s))", (type_id, parameter_ids))
    for parameter_id in parameter_ids:
        cur.execute(
            """
            INSERT INTO sensor_type_parameters(type_id, parameter_id)
            SELECT %s, %s
            WHERE NOT EXISTS (
                SELECT 1 FROM sensor_type_parameters WHERE type_id = %s AND parameter_id = %s
            )
            """,
            (type_id, parameter_id, type_id, parameter_id),
        )
    return type_id


def ensure_environment(cur) -> int:
    row = cur.execute("SELECT id FROM environments WHERE name = %s ORDER BY id LIMIT 1", (DEMO_ENVIRONMENT_NAME,)).fetchone()
    if row:
        env_id = int(row[0])
        cur.execute("UPDATE environments SET icon = %s WHERE id = %s", ("industrial", env_id))
        return env_id

    return int(cur.execute(
        "INSERT INTO environments(name, icon) VALUES (%s, %s) RETURNING id",
        (DEMO_ENVIRONMENT_NAME, "industrial"),
    ).fetchone()[0])


def ensure_room(cur, env_id: int, room_id: int | None) -> int:
    if room_id is not None:
        row = cur.execute("SELECT id FROM rooms WHERE id = %s", (room_id,)).fetchone()
        if row:
            cur.execute(
                "UPDATE rooms SET name = %s, icon = %s, environment_id = %s WHERE id = %s",
                (ROOM_NAME, "production", env_id, room_id),
            )
            return room_id

    row = cur.execute(
        "SELECT id FROM rooms WHERE environment_id = %s AND name = %s ORDER BY id LIMIT 1",
        (env_id, ROOM_NAME),
    ).fetchone()
    if row:
        return int(row[0])

    return int(cur.execute(
        "INSERT INTO rooms(name, environment_id, icon) VALUES (%s, %s, %s) RETURNING id",
        (ROOM_NAME, env_id, "production"),
    ).fetchone()[0])


def ensure_sensor(cur, serial: str, type_id: int, room_id: int) -> int:
    row = cur.execute("SELECT id FROM sensors WHERE serial_number = %s", (serial,)).fetchone()
    if row:
        sensor_id = int(row[0])
        cur.execute("UPDATE sensors SET type_id = %s, room_id = %s WHERE id = %s", (type_id, room_id, sensor_id))
        return sensor_id

    return int(cur.execute(
        "INSERT INTO sensors(serial_number, type_id, room_id, secret) VALUES (%s, %s, %s, %s) RETURNING id",
        (serial, type_id, room_id, md5(serial + serial)),
    ).fetchone()[0])


def ensure_device(cur, serial: str, room_id: int) -> int:
    row = cur.execute("SELECT id FROM devices WHERE serial_number = %s", (serial,)).fetchone()
    if row:
        device_id = int(row[0])
        cur.execute("UPDATE devices SET room_id = %s WHERE id = %s", (room_id, device_id))
        return device_id

    return int(cur.execute(
        "INSERT INTO devices(serial_number, room_id, secret) VALUES (%s, %s, %s) RETURNING id",
        (serial, room_id, md5(serial + serial)),
    ).fetchone()[0])


def build_layout(sensor_ids: list[int], sensor_serials: list[str], device_ids: list[int], device_serials: list[str]) -> dict:
    items = [
        {"id": "door-main", "type": "door", "label": "Service Door", "x": 0.0, "y": 4.4, "width": 0.32, "height": 1.4, "rotation": 90},
        {"id": "window-north", "type": "window", "label": "High Window", "x": 5.8, "y": 0.0, "width": 3.0, "height": 0.24, "rotation": 0},
        {"id": "operator-zone", "type": "zone", "label": "Operator Shift Zone", "x": 1.2, "y": 7.8, "width": 4.2, "height": 1.4, "rotation": 0},
    ]

    for machine in MACHINES:
        items.append({
            "id": f"machine-{machine.key}",
            "type": "equipment",
            "label": machine.label,
            "x": round(machine.center.x - machine.width / 2, 2),
            "y": round(machine.center.y - machine.height / 2, 2),
            "width": machine.width,
            "height": machine.height,
            "rotation": machine.rotation,
            "heat_load_kw": machine.heat_load_kw,
            "thermal_load": "high" if machine.heat_load_kw >= 18 else "medium",
        })

    for placement, sensor_id, serial in zip(SENSORS, sensor_ids, sensor_serials, strict=True):
        items.append({
            "id": f"sensor-{sensor_id}",
            "type": "sensor",
            "label": placement.label,
            "sensor_id": sensor_id,
            "serial_number": serial,
            "x": round(placement.point.x - 0.28, 2),
            "y": round(placement.point.y - 0.28, 2),
            "width": 0.56,
            "height": 0.56,
            "rotation": 0,
        })

    for placement, device_id, serial in zip(VENTS, device_ids, device_serials, strict=True):
        items.append({
            "id": f"vent-{device_id}",
            "type": "vent",
            "label": placement.label,
            "device_id": device_id,
            "serial_number": serial,
            "x": placement.x,
            "y": placement.y,
            "width": 0.8,
            "height": 0.8,
            "rotation": placement.rotation,
        })

    return {
        "width": 18,
        "height": 10,
        "unit": "m",
        "geometry": {
            "type": "rectangle",
            "points": [{"x": 0, "y": 0}, {"x": 18, "y": 0}, {"x": 18, "y": 10}, {"x": 0, "y": 10}],
        },
        "items": items,
    }


def shift_occupancy(ts: datetime, rng: random.Random) -> float:
    weekday = ts.weekday()
    hour = ts.hour + ts.minute / 60
    if weekday >= 5:
        base = 3.5 if 9 <= hour < 15 else 1.0
    elif 6 <= hour < 14:
        base = 17.0
    elif 14 <= hour < 22:
        base = 14.0
    else:
        base = 4.0

    if 11.75 <= hour <= 12.5 or 18.0 <= hour <= 18.45:
        base *= 0.62
    if any(abs(hour - shift) < 0.22 for shift in (6, 14, 22)):
        base += 3.0
    return clamp(base + rng.uniform(-1.2, 1.5), 0, 28)


def machine_load(machine: Machine, ts: datetime, rng: random.Random) -> float:
    weekday = ts.weekday()
    hour = ts.hour + ts.minute / 60
    if weekday >= 5:
        weekend_factor = 0.2 if 9 <= hour < 15 else 0.05
    else:
        weekend_factor = 1.0

    cycle = 0.5 + 0.5 * math.sin((ts.timestamp() / 60) / 47.0 + len(machine.key))
    if machine.profile == "batch_heat":
        scheduled = 0.9 if (7.5 <= hour < 12.2 or 15.0 <= hour < 20.0) else 0.18
        return clamp((scheduled + 0.18 * cycle + rng.uniform(-0.04, 0.05)) * weekend_factor, 0, 1)
    if machine.profile == "continuous":
        scheduled = 0.62 if 6 <= hour < 22 else 0.34
        return clamp((scheduled + 0.12 * cycle + rng.uniform(-0.05, 0.04)) * max(weekend_factor, 0.45), 0, 1)

    scheduled = 0.82 if 6 <= hour < 22 else 0.12
    if 12.0 <= hour <= 12.5 or 18.0 <= hour <= 18.5:
        scheduled *= 0.58
    return clamp((scheduled + 0.15 * cycle + rng.uniform(-0.04, 0.05)) * weekend_factor, 0, 1)


def gaussian(distance: float, sigma: float) -> float:
    return math.exp(-((distance / sigma) ** 2))


def vent_influence(point: Point, fan_avg: float) -> float:
    influence = 0.0
    for vent in VENTS:
        source = Point(vent.x + 0.4, vent.y + 0.4)
        dx = source.x - point.x
        dy = point.y - source.y
        downstream = max(0.0, dx)
        lateral = abs(dy)
        influence += gaussian(downstream, 8.5) * gaussian(lateral, 2.1)
    return clamp(influence * (fan_avg / 100), 0, 1)


def parse_end_at(value: str | None) -> datetime | None:
    if not value:
        return None
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        return parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def seed_history(
    cur,
    room_id: int,
    sensor_ids: list[int],
    device_ids: list[int],
    parameter_ids: dict[str, int],
    days: int,
    interval_minutes: int,
    seed: int,
    end_at: datetime | None = None,
) -> dict:
    rng = random.Random(seed)
    end = (end_at or datetime.now(timezone.utc)).replace(second=0, microsecond=0)
    end -= timedelta(minutes=end.minute % interval_minutes)
    start = end - timedelta(days=days)

    cur.execute("DELETE FROM sensor_data WHERE sensor_id = ANY(%s)", (sensor_ids,))
    cur.execute("DELETE FROM device_data WHERE device_id = ANY(%s)", (device_ids,))

    sensor_rows = []
    device_rows = []
    co2 = 520.0
    room_temp = 24.5
    humidity = 48.0
    fan_1 = 24.0
    fan_2 = 28.0
    failure_windows = {
        start + timedelta(days=9, hours=6): start + timedelta(days=9, hours=7, minutes=35),
        start + timedelta(days=22, hours=15): start + timedelta(days=22, hours=16, minutes=20),
    }

    timestamp = start
    steps = 0
    while timestamp <= end:
        occupancy = shift_occupancy(timestamp, rng)
        loads = {machine.key: machine_load(machine, timestamp, rng) for machine in MACHINES}
        heat_kw = sum(machine.heat_load_kw * loads[machine.key] for machine in MACHINES)
        outdoor_temp = 17.0 + 8.0 * math.sin(((timestamp.hour + timestamp.minute / 60) - 14) / 24 * 2 * math.pi)
        dt = interval_minutes / 5.0
        failure = any(window_start <= timestamp <= window_end for window_start, window_end in failure_windows.items())

        target_fan = 10 + max(0, co2 - 680) * 0.045 + max(0, room_temp - 29.0) * 5.0 + rng.uniform(-3.0, 4.0)
        if timestamp.weekday() >= 5:
            target_fan -= 8
        if failure:
            target_fan = min(target_fan, 12)
        target_fan = clamp(target_fan, 4, 88)
        fan_1 += (target_fan - fan_1) * 0.26 + rng.uniform(-1.2, 1.4)
        fan_2 += (target_fan + 6 - fan_2) * 0.22 + rng.uniform(-1.5, 1.3)
        fan_1 = clamp(fan_1, 0, 100)
        fan_2 = clamp(fan_2, 0, 100)
        fan_avg = (fan_1 + fan_2) / 2

        co2 += occupancy * 3.15 * dt - (co2 - 420) * (0.026 + fan_avg * 0.0009) * dt + rng.uniform(-7, 8)
        co2 = clamp(co2, 410, 2100)
        room_temp += heat_kw * 0.011 * dt + occupancy * 0.014 * dt - fan_avg * 0.0075 * dt + (outdoor_temp - room_temp) * 0.008 * dt + rng.uniform(-0.04, 0.05)
        room_temp = clamp(room_temp, 18, 39)
        humidity += occupancy * 0.035 * dt - fan_avg * 0.006 * dt + (48 - humidity) * 0.018 * dt + rng.uniform(-0.12, 0.14)
        humidity = clamp(humidity, 30, 75)

        machine_heat_by_key = {machine.key: machine.heat_load_kw * loads[machine.key] for machine in MACHINES}
        for sensor_id, placement in zip(sensor_ids, SENSORS, strict=True):
            local_heat = 0.0
            for machine in MACHINES:
                distance = math.hypot(placement.point.x - machine.center.x, placement.point.y - machine.center.y)
                local_heat += machine_heat_by_key[machine.key] * 0.13 * gaussian(distance, 3.3)
            airflow = vent_influence(placement.point, fan_avg)
            temp_value = room_temp + local_heat - airflow * (0.6 + fan_avg * 0.008) + rng.uniform(-0.12, 0.12)
            co2_value = co2 + (occupancy - 8) * (5.5 if placement.serial_suffix != "-3" else 2.4) - airflow * 80 + rng.uniform(-18, 18)
            humidity_value = humidity + occupancy * 0.08 - airflow * 1.5 - max(0, local_heat) * 0.08 + rng.uniform(-0.55, 0.55)
            naive_ts = timestamp.replace(tzinfo=None)
            sensor_rows.extend([
                (sensor_id, parameter_ids["temperature"], naive_ts, round(clamp(temp_value, 16, 45), 2), naive_ts),
                (sensor_id, parameter_ids["humidity"], naive_ts, round(clamp(humidity_value, 15, 95), 2), naive_ts),
                (sensor_id, parameter_ids["co2"], naive_ts, round(clamp(co2_value, 380, 5000), 2), naive_ts),
            ])

        naive_ts = timestamp.replace(tzinfo=None)
        device_rows.append((device_ids[0], naive_ts, round(fan_1, 2), True, naive_ts))
        device_rows.append((device_ids[1], naive_ts, round(fan_2, 2), True, naive_ts))
        timestamp += timedelta(minutes=interval_minutes)
        steps += 1

    cur.executemany(
        "INSERT INTO sensor_data(sensor_id, parameter_id, timestamp, value, sent_at) VALUES (%s, %s, %s, %s, %s)",
        sensor_rows,
    )
    cur.executemany(
        "INSERT INTO device_data(device_id, timestamp, value, applied, applied_at) VALUES (%s, %s, %s, %s, %s)",
        device_rows,
    )
    return {
        "room_id": room_id,
        "from": start.isoformat(),
        "to": end.isoformat(),
        "steps": steps,
        "sensor_rows": len(sensor_rows),
        "device_rows": len(device_rows),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed a month-long industrial demo telemetry dataset.")
    parser.add_argument("--room-id", type=int, default=1)
    parser.add_argument("--days", type=int, default=30)
    parser.add_argument("--interval-minutes", type=int, default=5)
    parser.add_argument("--seed", type=int, default=20260619)
    parser.add_argument("--end-at", default=None, help="Inclusive UTC end timestamp, for example 2026-06-19T23:55:00Z")
    args = parser.parse_args()

    if args.days < 1 or args.days > 90:
        raise SystemExit("--days must be between 1 and 90")
    if args.interval_minutes < 1 or args.interval_minutes > 60:
        raise SystemExit("--interval-minutes must be between 1 and 60")

    with psycopg.connect(database_url(), autocommit=False) as conn:
        with conn.cursor() as cur:
            parameter_ids = {
                name: ensure_parameter(cur, name, unit, min_value, max_value)
                for name, (unit, min_value, max_value) in PARAMETERS.items()
            }
            sensor_type_id = ensure_sensor_type(cur, list(parameter_ids.values()))
            env_id = ensure_environment(cur)
            room_id = ensure_room(cur, env_id, args.room_id)

            sensor_serials = [f"demo-room-{room_id}-microclimate{placement.serial_suffix}" for placement in SENSORS]
            device_serials = [f"demo-room-{room_id}-ventilation{placement.serial_suffix}" for placement in VENTS]
            sensor_ids = [ensure_sensor(cur, serial, sensor_type_id, room_id) for serial in sensor_serials]
            device_ids = [ensure_device(cur, serial, room_id) for serial in device_serials]
            layout = build_layout(sensor_ids, sensor_serials, device_ids, device_serials)
            cur.execute("UPDATE rooms SET layout = %s::jsonb WHERE id = %s", (json.dumps(layout), room_id))
            cur.execute(
                """
                INSERT INTO demo_room_profiles(room_id, scenario, updated_at)
                VALUES (%s, 'auto', CURRENT_TIMESTAMP)
                ON CONFLICT (room_id) DO UPDATE SET scenario = EXCLUDED.scenario, updated_at = CURRENT_TIMESTAMP
                """,
                (room_id,),
            )
            summary = seed_history(
                cur,
                room_id,
                sensor_ids,
                device_ids,
                parameter_ids,
                args.days,
                args.interval_minutes,
                args.seed,
                parse_end_at(args.end_at),
            )
        conn.commit()

    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
