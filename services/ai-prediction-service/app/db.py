from __future__ import annotations

import os
from urllib.parse import quote_plus

import psycopg


SCHEMA_SQL = """
CREATE TABLE IF NOT EXISTS ventilation_commands (
    id bigserial PRIMARY KEY,
    room_id int NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
    device_id int REFERENCES devices(id) ON DELETE SET NULL,
    timestamp timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    source varchar(64) NOT NULL,
    command_type varchar(64) NOT NULL,
    requested_power real,
    payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    status varchar(32) NOT NULL DEFAULT 'created'
);

CREATE INDEX IF NOT EXISTS idx_ventilation_commands_room_timestamp
    ON ventilation_commands(room_id, timestamp DESC);

CREATE TABLE IF NOT EXISTS ai_model_versions (
    id bigserial PRIMARY KEY,
    version varchar(128) NOT NULL UNIQUE,
    model_path varchar(512) NOT NULL,
    metrics jsonb NOT NULL DEFAULT '{}'::jsonb,
    trained_from timestamptz,
    trained_to timestamptz,
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    active bool NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS idx_ai_model_versions_active_created
    ON ai_model_versions(active, created_at DESC);
"""


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


def connect() -> psycopg.Connection:
    return psycopg.connect(database_url(), autocommit=True)


def ensure_schema() -> None:
    with connect() as conn:
        with conn.cursor() as cur:
            cur.execute(SCHEMA_SQL)
