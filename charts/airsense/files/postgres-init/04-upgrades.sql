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

CREATE TABLE IF NOT EXISTS "user_notifications" (
    "id" bigserial PRIMARY KEY,
    "user_id" int NOT NULL REFERENCES "users" ("id") ON DELETE CASCADE,
    "title" varchar(200) NOT NULL,
    "body" text NOT NULL,
    "severity" varchar(32) NOT NULL DEFAULT 'info',
    "data" jsonb NOT NULL DEFAULT '{}'::jsonb,
    "created_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "read_at" timestamptz
);

CREATE INDEX IF NOT EXISTS "idx_user_notifications_user_created"
    ON "user_notifications" ("user_id", "created_at" DESC, "id" DESC);

CREATE INDEX IF NOT EXISTS "idx_user_notifications_user_unread"
    ON "user_notifications" ("user_id", "created_at" DESC)
    WHERE "read_at" IS NULL;

CREATE TABLE IF NOT EXISTS "threshold_alert_states" (
    "room_id" int NOT NULL REFERENCES "rooms" ("id") ON DELETE CASCADE,
    "sensor_id" int NOT NULL REFERENCES "sensors" ("id") ON DELETE CASCADE,
    "parameter_id" int NOT NULL REFERENCES "parameters" ("id") ON DELETE CASCADE,
    "is_active" bool NOT NULL DEFAULT FALSE,
    "last_value" real NOT NULL,
    "critical_value" real NOT NULL,
    "triggered_at" timestamptz,
    "resolved_at" timestamptz,
    "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    PRIMARY KEY ("room_id", "sensor_id", "parameter_id")
);

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

CREATE TABLE IF NOT EXISTS "ai_control_settings" (
    "room_id" int PRIMARY KEY REFERENCES "rooms" ("id") ON DELETE CASCADE,
    "enabled" bool NOT NULL DEFAULT FALSE,
    "target_co2" real NOT NULL DEFAULT 900,
    "target_temperature" real,
    "target_humidity" real,
    "max_ventilation_power" real NOT NULL DEFAULT 100,
    "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);

CREATE TABLE IF NOT EXISTS "demo_room_profiles" (
    "room_id" int PRIMARY KEY REFERENCES "rooms" ("id") ON DELETE CASCADE,
    "scenario" varchar(64) NOT NULL DEFAULT 'auto',
    "ventilation_power_override" real,
    "occupancy_override" int,
    "updated_at" timestamptz NOT NULL DEFAULT (CURRENT_TIMESTAMP)
);

WITH seed_parameters(name, unit, min_value, max_value) AS (
    VALUES
        ('temperature', '°C', -50::real, 50::real),
        ('humidity', '%', 0::real, 100::real),
        ('pressure', 'hPa', 300::real, 1100::real),
        ('co2', 'ppm', 300::real, 5000::real)
)
INSERT INTO "parameters" ("name", "unit", "min_value", "max_value")
SELECT name, unit, min_value, max_value
FROM seed_parameters seed
WHERE NOT EXISTS (
    SELECT 1 FROM "parameters" p WHERE p.name = seed.name
);

UPDATE "parameters" p
SET unit = seed.unit,
    min_value = seed.min_value,
    max_value = seed.max_value
FROM (
    VALUES
        ('temperature', '°C', -50::real, 50::real),
        ('humidity', '%', 0::real, 100::real),
        ('pressure', 'hPa', 300::real, 1100::real),
        ('co2', 'ppm', 300::real, 5000::real)
) AS seed(name, unit, min_value, max_value)
WHERE p.name = seed.name
  AND (
    p.unit IS DISTINCT FROM seed.unit
    OR p.min_value IS DISTINCT FROM seed.min_value
    OR p.max_value IS DISTINCT FROM seed.max_value
  );

WITH seed_sensor_types(name) AS (
    VALUES
        ('Temperature Sensor'),
        ('Humidity Sensor'),
        ('Pressure Sensor'),
        ('Temperature and Humidity Sensor'),
        ('Air Quality Sensor'),
        ('Microclimate Sensor')
)
INSERT INTO "sensor_types" ("name")
SELECT name
FROM seed_sensor_types seed
WHERE NOT EXISTS (
    SELECT 1 FROM "sensor_types" st WHERE st.name = seed.name
);

WITH sensor_type_ids AS (
    SELECT DISTINCT ON (name) id, name
    FROM "sensor_types"
    ORDER BY name, id
),
parameter_ids AS (
    SELECT DISTINCT ON (name) id, name
    FROM "parameters"
    ORDER BY name, id
),
type_parameter_pairs(type_name, parameter_name) AS (
    VALUES
        ('Temperature Sensor', 'temperature'),
        ('Humidity Sensor', 'humidity'),
        ('Pressure Sensor', 'pressure'),
        ('Temperature and Humidity Sensor', 'temperature'),
        ('Temperature and Humidity Sensor', 'humidity'),
        ('Air Quality Sensor', 'co2'),
        ('Microclimate Sensor', 'temperature'),
        ('Microclimate Sensor', 'humidity'),
        ('Microclimate Sensor', 'co2')
)
INSERT INTO "sensor_type_parameters" ("type_id", "parameter_id")
SELECT st.id, p.id
FROM type_parameter_pairs pair
JOIN sensor_type_ids st ON st.name = pair.type_name
JOIN parameter_ids p ON p.name = pair.parameter_name
WHERE NOT EXISTS (
    SELECT 1
    FROM "sensor_type_parameters" existing
    WHERE existing.type_id = st.id
      AND existing.parameter_id = p.id
);

DELETE FROM "sensor_type_parameters" stp
USING "parameters" p
WHERE stp.parameter_id = p.id
  AND p.name = 'occupancy';

DELETE FROM "settings" s
USING "parameters" p
WHERE s.parameter_id = p.id
  AND p.name = 'occupancy';

DELETE FROM "sensor_data" sd
USING "parameters" p
WHERE sd.parameter_id = p.id
  AND p.name = 'occupancy';

DELETE FROM "parameters"
WHERE name = 'occupancy';

DELETE FROM "sensor_types" st
WHERE st.name = 'Occupancy Sensor'
  AND NOT EXISTS (
    SELECT 1 FROM "sensors" s WHERE s.type_id = st.id
  );

UPDATE "environments"
SET icon = 'industrial'
WHERE name = 'AirSense Demo Environment'
  AND icon IN ('factory', 'building', '');

UPDATE "rooms" r
SET icon = CASE
    WHEN r.icon = 'home' THEN 'office'
    ELSE 'production'
END
FROM "environments" e
WHERE r.environment_id = e.id
  AND e.name = 'AirSense Demo Environment'
  AND r.icon IN ('factory', 'home');

WITH demo_owner AS (
    INSERT INTO "users" ("uid", "name", "email")
    VALUES ('pending:khijnyak.dima@gmail.com', 'khijnyak.dima@gmail.com', 'khijnyak.dima@gmail.com')
    ON CONFLICT ("email") DO UPDATE SET
        "name" = EXCLUDED."name"
    RETURNING id
),
demo_env AS (
    SELECT id
    FROM "environments"
    WHERE name = 'AirSense Demo Environment'
    ORDER BY id
    LIMIT 1
)
INSERT INTO "environment_members" ("member_id", "environment_id", "role")
SELECT demo_owner.id, demo_env.id, 'owner'
FROM demo_owner
CROSS JOIN demo_env
ON CONFLICT ("member_id", "environment_id") DO UPDATE SET
    "role" = EXCLUDED."role";

WITH demo_env AS (
    SELECT id
    FROM "environments"
    WHERE name = 'AirSense Demo Environment'
    ORDER BY id
    LIMIT 1
)
INSERT INTO "environment_members" ("member_id", "environment_id", "role")
SELECT u.id, demo_env.id, 'user'
FROM "users" u
CROSS JOIN demo_env
WHERE NOT EXISTS (
    SELECT 1
    FROM "environment_members" em
    WHERE em.member_id = u.id
      AND em.environment_id = demo_env.id
);

WITH demo_env AS (
    SELECT id
    FROM "environments"
    WHERE name = 'AirSense Demo Environment'
    ORDER BY id
    LIMIT 1
),
microclimate_type AS (
    SELECT id
    FROM "sensor_types"
    WHERE name = 'Microclimate Sensor'
    ORDER BY id
    LIMIT 1
),
demo_room_slots AS (
    SELECT r.id AS room_id,
           row_number() OVER (ORDER BY r.id)::int AS demo_slot
    FROM "rooms" r
    JOIN demo_env e ON e.id = r.environment_id
),
desired_sensors AS (
    SELECT rs.room_id,
           mt.id AS type_id,
           'demo-room-' || rs.demo_slot || '-microclimate' ||
               CASE WHEN slot.slot = 1 THEN '' ELSE '-' || slot.slot END AS serial_number
    FROM demo_room_slots rs
    CROSS JOIN microclimate_type mt
    CROSS JOIN generate_series(1, 3) AS slot(slot)
)
INSERT INTO "sensors" ("serial_number", "type_id", "room_id", "secret")
SELECT ds.serial_number, ds.type_id, ds.room_id, md5(ds.serial_number || ds.serial_number)
FROM desired_sensors ds
WHERE NOT EXISTS (
    SELECT 1
    FROM "sensors" s
    WHERE s.serial_number = ds.serial_number
);

WITH demo_env AS (
    SELECT id
    FROM "environments"
    WHERE name = 'AirSense Demo Environment'
    ORDER BY id
    LIMIT 1
),
microclimate_type AS (
    SELECT id
    FROM "sensor_types"
    WHERE name = 'Microclimate Sensor'
    ORDER BY id
    LIMIT 1
),
demo_room_slots AS (
    SELECT r.id AS room_id,
           row_number() OVER (ORDER BY r.id)::int AS demo_slot
    FROM "rooms" r
    JOIN demo_env e ON e.id = r.environment_id
),
desired_sensors AS (
    SELECT rs.room_id,
           mt.id AS type_id,
           'demo-room-' || rs.demo_slot || '-microclimate' ||
               CASE WHEN slot.slot = 1 THEN '' ELSE '-' || slot.slot END AS serial_number
    FROM demo_room_slots rs
    CROSS JOIN microclimate_type mt
    CROSS JOIN generate_series(1, 3) AS slot(slot)
)
UPDATE "sensors" s
SET type_id = ds.type_id,
    room_id = ds.room_id
FROM desired_sensors ds
WHERE s.serial_number = ds.serial_number;

WITH demo_env AS (
    SELECT id
    FROM "environments"
    WHERE name = 'AirSense Demo Environment'
    ORDER BY id
    LIMIT 1
),
demo_room_slots AS (
    SELECT r.id AS room_id,
           row_number() OVER (ORDER BY r.id)::int AS demo_slot
    FROM "rooms" r
    JOIN demo_env e ON e.id = r.environment_id
),
desired_devices AS (
    SELECT rs.room_id,
           'demo-room-' || rs.demo_slot || '-ventilation' ||
               CASE WHEN slot.slot = 1 THEN '' ELSE '-' || slot.slot END AS serial_number
    FROM demo_room_slots rs
    CROSS JOIN generate_series(1, 2) AS slot(slot)
)
INSERT INTO "devices" ("serial_number", "room_id", "secret")
SELECT dd.serial_number, dd.room_id, md5(dd.serial_number || dd.serial_number)
FROM desired_devices dd
WHERE NOT EXISTS (
    SELECT 1
    FROM "devices" d
    WHERE d.serial_number = dd.serial_number
);

WITH demo_env AS (
    SELECT id
    FROM "environments"
    WHERE name = 'AirSense Demo Environment'
    ORDER BY id
    LIMIT 1
),
demo_room_slots AS (
    SELECT r.id AS room_id,
           row_number() OVER (ORDER BY r.id)::int AS demo_slot
    FROM "rooms" r
    JOIN demo_env e ON e.id = r.environment_id
),
desired_devices AS (
    SELECT rs.room_id,
           'demo-room-' || rs.demo_slot || '-ventilation' ||
               CASE WHEN slot.slot = 1 THEN '' ELSE '-' || slot.slot END AS serial_number
    FROM demo_room_slots rs
    CROSS JOIN generate_series(1, 2) AS slot(slot)
)
UPDATE "devices" d
SET room_id = dd.room_id
FROM desired_devices dd
WHERE d.serial_number = dd.serial_number;

WITH room_scope AS (
    SELECT r.id AS room_id,
           r.layout,
           row_number() OVER (ORDER BY r.id) AS demo_index
    FROM "rooms" r
    JOIN "environments" e ON e.id = r.environment_id
    WHERE e.name = 'AirSense Demo Environment'
),
demo_rooms AS (
    SELECT rs.room_id,
           rs.layout,
           (mod((rs.demo_index - 1), 4) + 1)::int AS profile_index,
           CASE (mod((rs.demo_index - 1), 4) + 1)::int
               WHEN 1 THEN 18::numeric
               WHEN 2 THEN 10::numeric
               WHEN 3 THEN 12::numeric
               ELSE 14::numeric
           END AS width,
           CASE (mod((rs.demo_index - 1), 4) + 1)::int
               WHEN 1 THEN 9::numeric
               WHEN 2 THEN 7::numeric
               WHEN 3 THEN 8::numeric
               ELSE 7::numeric
           END AS height,
           'm' AS unit,
           CASE (mod((rs.demo_index - 1), 4) + 1)::int
               WHEN 1 THEN '{"type":"rectangle","points":[{"x":0,"y":0},{"x":18,"y":0},{"x":18,"y":9},{"x":0,"y":9}]}'::jsonb
               WHEN 2 THEN '{"type":"l_shape","points":[{"x":0,"y":0},{"x":10,"y":0},{"x":10,"y":4.2},{"x":6.6,"y":4.2},{"x":6.6,"y":7},{"x":0,"y":7}]}'::jsonb
               WHEN 3 THEN '{"type":"t_shape","points":[{"x":3.2,"y":0},{"x":8.8,"y":0},{"x":8.8,"y":2.2},{"x":12,"y":2.2},{"x":12,"y":5.8},{"x":8.8,"y":5.8},{"x":8.8,"y":8},{"x":3.2,"y":8},{"x":3.2,"y":5.8},{"x":0,"y":5.8},{"x":0,"y":2.2},{"x":3.2,"y":2.2}]}'::jsonb
               ELSE '{"type":"custom","points":[{"x":0,"y":0},{"x":14,"y":0},{"x":14,"y":5.6},{"x":11.8,"y":5.6},{"x":11.8,"y":7},{"x":2.1,"y":7},{"x":2.1,"y":5.9},{"x":0,"y":5.9}]}'::jsonb
           END AS geometry,
           CASE
               WHEN jsonb_typeof(rs.layout -> 'items') = 'array' THEN rs.layout -> 'items'
               ELSE '[]'::jsonb
           END AS items
    FROM room_scope rs
),
current_items AS (
    SELECT dr.room_id,
           item.value AS item,
           item.ordinality
    FROM demo_rooms dr
    LEFT JOIN LATERAL jsonb_array_elements(dr.items) WITH ORDINALITY AS item(value, ordinality) ON TRUE
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
    SELECT dr.room_id,
           COALESCE(jsonb_agg(profile_item.item ORDER BY profile_item.sort_key), '[]'::jsonb) AS items
    FROM demo_rooms dr
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
        WHERE profile_item.profile_index = dr.profile_index
    ) profile_item
    GROUP BY dr.room_id
),
ranked_sensors AS (
    SELECT dr.room_id,
           dr.width,
           dr.height,
           dr.profile_index,
           s.id,
           s.serial_number,
           row_number() OVER (PARTITION BY dr.room_id ORDER BY s.id) AS rn
    FROM demo_rooms dr
    JOIN "sensors" s ON s.room_id = dr.room_id
),
ranked_devices AS (
    SELECT dr.room_id,
           dr.width,
           dr.height,
           dr.profile_index,
           d.id,
           d.serial_number,
           row_number() OVER (PARTITION BY dr.room_id ORDER BY d.id) AS rn
    FROM demo_rooms dr
    JOIN "devices" d ON d.room_id = dr.room_id
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
    SELECT dr.room_id,
           jsonb_build_object(
               'width', dr.width,
               'height', dr.height,
               'unit', dr.unit,
               'geometry', dr.geometry,
               'demo_template_version', 'rich-demo-v2',
               'items', COALESCE(t.items, '[]'::jsonb) || COALESCE(p.items, '[]'::jsonb) || COALESCE(g.items, '[]'::jsonb)
           ) AS layout
    FROM demo_rooms dr
    LEFT JOIN template_items t ON t.room_id = dr.room_id
    LEFT JOIN preserved_items p ON p.room_id = dr.room_id
    LEFT JOIN generated_items g ON g.room_id = dr.room_id
)
UPDATE "rooms" r
SET layout = next_layouts.layout
FROM next_layouts
WHERE r.id = next_layouts.room_id
  AND r.layout IS DISTINCT FROM next_layouts.layout;
