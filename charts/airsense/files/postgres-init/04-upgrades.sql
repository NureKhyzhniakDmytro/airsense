ALTER TABLE "environments"
    ADD COLUMN IF NOT EXISTS "icon" varchar(64) NOT NULL DEFAULT 'building';

ALTER TABLE "rooms"
    ADD COLUMN IF NOT EXISTS "icon" varchar(64) NOT NULL DEFAULT 'room';

ALTER TABLE "rooms"
    ADD COLUMN IF NOT EXISTS "layout" jsonb NOT NULL DEFAULT '{"width":6,"height":4,"unit":"m","geometry":{"type":"rectangle","points":[{"x":0,"y":0},{"x":6,"y":0},{"x":6,"y":4},{"x":0,"y":4}]},"items":[]}'::jsonb;

CREATE TABLE IF NOT EXISTS "ventilation_commands" (
    "id" bigserial PRIMARY KEY,
    "room_id" int NOT NULL REFERENCES "rooms" ("id") ON DELETE CASCADE,
    "device_id" int REFERENCES "devices" ("id") ON DELETE SET NULL,
    "timestamp" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "source" varchar(64) NOT NULL,
    "command_type" varchar(64) NOT NULL,
    "requested_power" real,
    "payload" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "status" varchar(32) NOT NULL DEFAULT 'created'
);

CREATE INDEX IF NOT EXISTS "idx_ventilation_commands_room_timestamp"
    ON "ventilation_commands" ("room_id", "timestamp" DESC);

CREATE TABLE IF NOT EXISTS "ai_model_versions" (
    "id" bigserial PRIMARY KEY,
    "version" varchar(128) UNIQUE NOT NULL,
    "model_path" varchar(512) NOT NULL,
    "metrics" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "trained_from" timestamptz,
    "trained_to" timestamptz,
    "created_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "active" bool NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS "idx_ai_model_versions_active_created"
    ON "ai_model_versions" ("active", "created_at" DESC);

CREATE TABLE IF NOT EXISTS "demo_room_profiles" (
    "room_id" int PRIMARY KEY REFERENCES "rooms" ("id") ON DELETE CASCADE,
    "scenario" varchar(64) NOT NULL DEFAULT 'auto',
    "ventilation_power_override" real,
    "occupancy_override" int,
    "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);
