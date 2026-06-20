CREATE TABLE "users" (
                         "id" serial PRIMARY KEY,
                         "name" varchar NOT NULL,
                         "uid" varchar UNIQUE NOT NULL,
                         "email" varchar(100) UNIQUE NOT NULL,
                         "notification_token" varchar,
                         "created_at" timestamp NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE "environments" (
                                "id" serial PRIMARY KEY,
                                "name" varchar NOT NULL,
                                "icon" varchar(64) NOT NULL DEFAULT 'building'
);

CREATE TABLE "rooms" (
                         "id" serial PRIMARY KEY,
                         "name" varchar NOT NULL,
                         "environment_id" int NOT NULL,
                         "icon" varchar(64) NOT NULL DEFAULT 'room',
                         "layout" jsonb NOT NULL DEFAULT '{"width":6,"height":4,"unit":"m","geometry":{"type":"rectangle","points":[{"x":0,"y":0},{"x":6,"y":0},{"x":6,"y":4},{"x":0,"y":4}]},"items":[]}'::jsonb
);

CREATE TABLE "settings" (
                            "room_id" int NOT NULL,
                            "parameter_id" int NOT NULL,
                            "curve" json NOT NULL,
                            PRIMARY KEY ("room_id", "parameter_id")
);

CREATE TABLE "sensors" (
                           "id" serial PRIMARY KEY,
                           "serial_number" varchar NOT NULL,
                           "room_id" int,
                           "type_id" int NOT NULL,
                           "secret" varchar NOT NULL
);

CREATE TABLE "sensor_types" (
                                "id" serial PRIMARY KEY,
                                "name" varchar NOT NULL
);

CREATE TABLE "environment_members" (
                                       "member_id" int NOT NULL,
                                       "environment_id" int NOT NULL,
                                       "role" varchar,
                                       PRIMARY KEY ("member_id", "environment_id")
);

CREATE TABLE "devices" (
                           "id" serial PRIMARY KEY,
                           "serial_number" varchar NOT NULL,
                           "room_id" int,
                           "secret" varchar NOT NULL
);

CREATE TABLE "device_data" (
                               "id" serial PRIMARY KEY,
                               "device_id" int NOT NULL,
                               "timestamp" timestamp NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                               "value" real NOT NULL,
                               "applied" bool NOT NULL DEFAULT FALSE,
                               "applied_at" timestamp
);

CREATE TABLE "sensor_data" (
                               "id" serial PRIMARY KEY,
                               "sensor_id" int NOT NULL,
                               "timestamp" timestamp NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                               "parameter_id" int NOT NULL,
                               "value" real NOT NULL,
                               "sent_at" timestamp NOT NULL
);

CREATE TABLE "ventilation_commands" (
                                        "id" bigserial PRIMARY KEY,
                                        "room_id" int NOT NULL,
                                        "device_id" int,
                                        "timestamp" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                                        "source" varchar(64) NOT NULL,
                                        "command_type" varchar(64) NOT NULL,
                                        "requested_power" real,
                                        "payload" jsonb NOT NULL DEFAULT '{}'::jsonb,
                                        "status" varchar(32) NOT NULL DEFAULT 'created'
);

CREATE INDEX "idx_ventilation_commands_room_timestamp"
    ON "ventilation_commands" ("room_id", "timestamp" DESC);

CREATE TABLE "user_notifications" (
                                      "id" bigserial PRIMARY KEY,
                                      "user_id" int NOT NULL,
                                      "title" varchar(200) NOT NULL,
                                      "body" text NOT NULL,
                                      "severity" varchar(32) NOT NULL DEFAULT 'info',
                                      "data" jsonb NOT NULL DEFAULT '{}'::jsonb,
                                      "created_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                                      "read_at" timestamptz
);

CREATE INDEX "idx_user_notifications_user_created"
    ON "user_notifications" ("user_id", "created_at" DESC, "id" DESC);

CREATE INDEX "idx_user_notifications_user_unread"
    ON "user_notifications" ("user_id", "created_at" DESC)
    WHERE "read_at" IS NULL;

CREATE TABLE "threshold_alert_states" (
                                          "room_id" int NOT NULL,
                                          "sensor_id" int NOT NULL,
                                          "parameter_id" int NOT NULL,
                                          "is_active" bool NOT NULL DEFAULT FALSE,
                                          "last_value" real NOT NULL,
                                          "critical_value" real NOT NULL,
                                          "triggered_at" timestamptz,
                                          "resolved_at" timestamptz,
                                          "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                                          PRIMARY KEY ("room_id", "sensor_id", "parameter_id")
);

CREATE TABLE "ai_model_versions" (
                                     "id" bigserial PRIMARY KEY,
                                     "version" varchar(128) UNIQUE NOT NULL,
                                     "model_path" varchar(512) NOT NULL,
                                     "metrics" jsonb NOT NULL DEFAULT '{}'::jsonb,
                                     "trained_from" timestamptz,
                                     "trained_to" timestamptz,
                                     "created_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                                     "active" bool NOT NULL DEFAULT FALSE
);

CREATE INDEX "idx_ai_model_versions_active_created"
    ON "ai_model_versions" ("active", "created_at" DESC);

CREATE TABLE "ai_control_settings" (
                                      "room_id" int PRIMARY KEY,
                                      "enabled" bool NOT NULL DEFAULT FALSE,
                                      "target_co2" real NOT NULL DEFAULT 900,
                                      "target_temperature" real,
                                      "target_humidity" real,
                                      "max_ventilation_power" real NOT NULL DEFAULT 100,
                                      "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE "demo_room_profiles" (
                                      "room_id" int PRIMARY KEY,
                                      "scenario" varchar(64) NOT NULL DEFAULT 'auto',
                                      "ventilation_power_override" real,
                                      "occupancy_override" int,
                                      "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE "parameters" (
                              "id" serial PRIMARY KEY,
                              "name" varchar NOT NULL,
                              "unit" varchar NOT NULL,
                              "min_value" real NOT NULL,
                              "max_value" real NOT NULL
);

CREATE TABLE "sensor_type_parameters" (
                                   "type_id" int NOT NULL,
                                   "parameter_id" int NOT NULL
);

ALTER TABLE "rooms" ADD FOREIGN KEY ("environment_id") REFERENCES "environments" ("id") ON DELETE CASCADE;

ALTER TABLE "settings" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE CASCADE;

ALTER TABLE "settings" ADD FOREIGN KEY ("parameter_id") REFERENCES "parameters" ("id") ON DELETE CASCADE;

ALTER TABLE "sensors" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE SET NULL;

ALTER TABLE "sensors" ADD FOREIGN KEY ("type_id") REFERENCES "sensor_types" ("id");

ALTER TABLE "environment_members" ADD FOREIGN KEY ("member_id") REFERENCES "users" ("id") ON DELETE CASCADE;

ALTER TABLE "environment_members" ADD FOREIGN KEY ("environment_id") REFERENCES "environments" ("id") ON DELETE CASCADE;

ALTER TABLE "devices" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE SET NULL;

ALTER TABLE "device_data" ADD FOREIGN KEY ("device_id") REFERENCES "devices" ("id") ON DELETE CASCADE;

ALTER TABLE "sensor_data" ADD FOREIGN KEY ("sensor_id") REFERENCES "sensors" ("id") ON DELETE CASCADE;

ALTER TABLE "sensor_data" ADD FOREIGN KEY ("parameter_id") REFERENCES "parameters" ("id") ON DELETE CASCADE;

ALTER TABLE "sensor_type_parameters" ADD FOREIGN KEY ("type_id") REFERENCES "sensor_types" ("id") ON DELETE CASCADE;

ALTER TABLE "sensor_type_parameters" ADD FOREIGN KEY ("parameter_id") REFERENCES "parameters" ("id");

ALTER TABLE "ventilation_commands" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE CASCADE;

ALTER TABLE "ventilation_commands" ADD FOREIGN KEY ("device_id") REFERENCES "devices" ("id") ON DELETE SET NULL;

ALTER TABLE "user_notifications" ADD FOREIGN KEY ("user_id") REFERENCES "users" ("id") ON DELETE CASCADE;

ALTER TABLE "threshold_alert_states" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE CASCADE;

ALTER TABLE "threshold_alert_states" ADD FOREIGN KEY ("sensor_id") REFERENCES "sensors" ("id") ON DELETE CASCADE;

ALTER TABLE "threshold_alert_states" ADD FOREIGN KEY ("parameter_id") REFERENCES "parameters" ("id") ON DELETE CASCADE;

ALTER TABLE "demo_room_profiles" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE CASCADE;

ALTER TABLE "ai_control_settings" ADD FOREIGN KEY ("room_id") REFERENCES "rooms" ("id") ON DELETE CASCADE;
