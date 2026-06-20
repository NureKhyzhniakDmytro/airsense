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

WITH demo_rooms AS (
    SELECT r.id AS room_id,
           r.layout,
           CASE
               WHEN (r.layout ->> 'width') ~ '^[0-9]+(\.[0-9]+)?$' THEN (r.layout ->> 'width')::numeric
               ELSE 6::numeric
           END AS width,
           CASE
               WHEN (r.layout ->> 'height') ~ '^[0-9]+(\.[0-9]+)?$' THEN (r.layout ->> 'height')::numeric
               ELSE 4::numeric
           END AS height,
           COALESCE(NULLIF(r.layout ->> 'unit', ''), 'm') AS unit,
           CASE
               WHEN jsonb_typeof(r.layout -> 'geometry') = 'object' THEN r.layout -> 'geometry'
               ELSE '{"type":"rectangle","points":[{"x":0,"y":0},{"x":6,"y":0},{"x":6,"y":4},{"x":0,"y":4}]}'::jsonb
           END AS geometry,
           CASE
               WHEN jsonb_typeof(r.layout -> 'items') = 'array' THEN r.layout -> 'items'
               ELSE '[]'::jsonb
           END AS items
    FROM "rooms" r
    JOIN "environments" e ON e.id = r.environment_id
    WHERE e.name = 'AirSense Demo Environment'
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
                   FILTER (WHERE item IS NOT NULL AND COALESCE(lower(item ->> 'type'), '') NOT IN ('sensor', 'vent')),
               '[]'::jsonb
           ) AS items
    FROM current_items
    GROUP BY room_id
),
ranked_sensors AS (
    SELECT dr.room_id,
           dr.width,
           dr.height,
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
           d.id,
           d.serial_number,
           row_number() OVER (PARTITION BY dr.room_id ORDER BY d.id) AS rn
    FROM demo_rooms dr
    JOIN "devices" d ON d.room_id = dr.room_id
),
existing_sensor_items AS (
    SELECT s.room_id,
           10 AS sort_group,
           ci.ordinality AS sort_key,
           (ci.item - 'device_id') || jsonb_build_object(
               'id', 'sensor-' || s.id,
               'type', 'sensor',
               'label', COALESCE(NULLIF(ci.item ->> 'label', ''), 'Sensor #' || s.id),
               'sensor_id', s.id,
               'serial_number', s.serial_number
           ) AS item
    FROM ranked_sensors s
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
               'label', 'Sensor #' || s.id,
               'sensor_id', s.id,
               'serial_number', s.serial_number,
               'x', round(greatest(0::numeric, least(s.width - 0.55, 0.45 + (((s.rn - 1) % 4)::numeric * 1.15))), 2),
               'y', round(greatest(0::numeric, least(s.height - 0.55, 0.55 + (((s.rn - 1) / 4)::numeric * 0.85))), 2),
               'width', 0.55,
               'height', 0.55,
               'rotation', 0
           ) AS item
    FROM ranked_sensors s
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
               'label', COALESCE(NULLIF(ci.item ->> 'label', ''), 'Vent #' || d.id),
               'device_id', d.id,
               'serial_number', d.serial_number
           ) AS item
    FROM ranked_devices d
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
               'label', 'Vent #' || d.id,
               'device_id', d.id,
               'serial_number', d.serial_number,
               'x', round(greatest(0::numeric, least(d.width - 0.75, d.width - 1.2 - (((d.rn - 1) % 3)::numeric * 1.1))), 2),
               'y', round(greatest(0::numeric, least(d.height - 0.75, 0.55 + (((d.rn - 1) / 3)::numeric * 1.0))), 2),
               'width', 0.75,
               'height', 0.75,
               'rotation', 0
           ) AS item
    FROM ranked_devices d
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
               'items', COALESCE(p.items, '[]'::jsonb) || COALESCE(g.items, '[]'::jsonb)
           ) AS layout
    FROM demo_rooms dr
    LEFT JOIN preserved_items p ON p.room_id = dr.room_id
    LEFT JOIN generated_items g ON g.room_id = dr.room_id
)
UPDATE "rooms" r
SET layout = next_layouts.layout
FROM next_layouts
WHERE r.id = next_layouts.room_id
  AND r.layout IS DISTINCT FROM next_layouts.layout;
