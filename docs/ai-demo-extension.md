# AI extension with demo device telemetry

## Folder Structure

```text
services/
  device-telemetry-simulator/
    app/simulator.py
    Dockerfile
    requirements.txt
  ai-prediction-service/
    app/main.py
    app/training.py
    app/model_store.py
    app/db.py
    app/schemas.py
    Dockerfile
    requirements.txt
charts/airsense/
  templates/ai.yaml
  values.yaml
api/
  Airsense.API/
    Controllers/AiController.cs
```

## Database Tables

The simulator does not store telemetry in a separate data category. It creates ordinary demo entities in the existing domain model:

- `environments`;
- `rooms`;
- `sensors`;
- `devices`;
- `sensor_data`;
- `device_data`;
- `settings`.

The AI extension adds only infrastructure tables:

```sql
CREATE TABLE ventilation_commands (
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

CREATE TABLE ai_model_versions (
    id bigserial PRIMARY KEY,
    version varchar(128) NOT NULL UNIQUE,
    model_path varchar(512) NOT NULL,
    metrics jsonb NOT NULL DEFAULT '{}'::jsonb,
    trained_from timestamptz,
    trained_to timestamptz,
    created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    active bool NOT NULL DEFAULT FALSE
);
```

## Device Telemetry Simulator

`device-telemetry-simulator` is a demo data source. It does not mark its measurements as a separate data class in the application model. The service prepares normal demo rooms, sensors, devices and automation curves, then publishes MQTT messages in the same format as physical sensors:

```text
sensor/{parameter}
```

MQTT user property:

```text
serial-number: demo-room-1-microclimate
```

Payload:

```json
{
  "value": 824.5,
  "sent_at": 1781792400
}
```

Published parameters:

- `co2`;
- `temperature`;
- `humidity`.

The simulator uses scenario-based generation:

- `empty_room`;
- `normal_usage`;
- `crowded_room`;
- `ventilation_failure`;
- `night_mode`;
- `critical_co2_event`.

CO2 grows when the simulator's internal occupancy load increases and falls when effective ventilation grows. Temperature and humidity change according to internal load, equipment heat, supply/exhaust ventilation influence and measurement noise. Occupancy is not published as sensor telemetry; it is only a demo-service scenario/profile input. The current supply/exhaust ventilation levels are reflected in ordinary `device_data` rows, so the rest of the system sees regular device history.

The simulator also reads room layouts. Sensor coordinates, ventilation device role/rotation and equipment `heat_load_kw` values affect generated readings, which creates spatial variation between sensors in the same room. This is a simplified engineering model, not a CFD calculation. The external basis and limitations are recorded in `docs/air-quality-research.md`.

The demo topology uses the same frontend icon and unit contract as normal user-created data:

- environment icon: `industrial`;
- alternating default room icons: `production`, `office`;
- temperature unit: `°C`;
- demo sensor type: `Microclimate Sensor`;
- parameters: `co2`, `temperature`, `humidity`.

## Demo Data Control UI

The web application includes a dedicated dashboard page:

```text
/dashboard/demo-data
```

This page allows an authenticated user to:

- prepare the demo environment, rooms, sensors and ventilation devices;
- inspect live stream status and latest telemetry timestamps;
- generate historical telemetry for charts and model training;
- clear only demo history while keeping rooms and devices;
- assign room-level simulation profiles;
- set optional internal occupancy-load and ventilation overrides.

The UI does not create a separate telemetry type. All values remain ordinary `sensor_data` and `device_data` rows. Room-level controls are stored in `demo_room_profiles`, which is a simulator configuration table, not a telemetry classification mechanism.

## REST API AI Prediction Service

## Training Data Sources

`AI Training Job` builds a supervised time-series dataset from two sources:

- ordinary AirSense telemetry in PostgreSQL (`sensor_data` and `device_data`);
- optional normalized public datasets from `services/ai-prediction-service/datasets/normalized`.

External rows are not inserted into the application telemetry tables. They are read only during training through the same AirSense feature contract: `co2`, `temperature`, `humidity`, `ventilation_power`, `supply_ventilation_power`, `exhaust_ventilation_power`, time features and `room_id`. Synthetic/demo snapshots can be exported to `services/ai-prediction-service/datasets/synthetic` for reproducibility without adding a new telemetry class to the domain model.

The trained model is a scikit-learn `MultiOutputRegressor` over `HistGradientBoostingRegressor`. It predicts CO2, temperature and humidity for 10, 20 and 30 minute horizons and stores the active artifact as `model.joblib`.

The exact public and synthetic datasets used for the current training run are documented in `docs/ai-training-datasets.md`.

### `GET /health`

```json
{
  "status": "ok",
  "model_version": "heuristic-untrained",
  "mode": "heuristic"
}
```

### `GET /model/version`

```json
{
  "version": "rf-20260618143000",
  "mode": "trained",
  "metrics": {
    "co2": { "mae": 35.2, "rmse": 48.9 },
    "temperature": { "mae": 0.18, "rmse": 0.24 },
    "humidity": { "mae": 0.7, "rmse": 1.1 }
  }
}
```

### `POST /predict`

```json
{
  "sample": {
    "room_id": 3,
    "co2": 950,
    "temperature": 24.1,
    "humidity": 51.2,
    "ventilation_power": 35
  },
  "horizons_minutes": [10, 20, 30]
}
```

### `POST /simulate`

```json
{
  "sample": {
    "room_id": 3,
    "co2": 1200,
    "temperature": 25,
    "humidity": 55,
    "ventilation_power": 20
  },
  "scenarios": [
    { "label": "quiet", "ventilation_power": 30 },
    { "label": "boost", "ventilation_power": 80 }
  ],
  "horizons_minutes": [10, 20, 30]
}
```

### `POST /recommendation`

```json
{
  "sample": {
    "room_id": 3,
    "co2": 1300,
    "temperature": 24.5,
    "humidity": 52,
    "ventilation_power": 25
  },
  "target_co2": 900,
  "max_ventilation_power": 100,
  "horizon_minutes": 20
}
```

The AI prediction service returns a recommendation but does not publish MQTT commands:

```json
{
  "model_version": "heuristic-untrained",
  "mode": "heuristic",
  "suggested_ventilation_power": 80,
  "reason": "Expected CO2 is 850.0 ppm in 20 minutes with ventilation power 80%.",
  "predicted": { "horizon_minutes": 20, "co2": 850.0, "temperature": 24.2, "humidity": 51.1 },
  "mqtt_command_topic": "devices/{deviceId}/control",
  "sends_command": false
}
```

When the backend accepts a stored recommendation through `POST /ai/room/{roomId}/recommendations/{recommendationId}/accept`, the application immediately applies it:

1. locks the recommendation row;
2. resolves the target ventilation device in the room;
3. inserts a `device_data` command row;
4. marks the recommendation as `used`;
5. publishes a `room/{roomId}` MQTT command with the requested fan speed.

Read-only users can view AI forecasts and recommendation history. Only owners/admins can generate or apply recommendations.

## Report Description In Ukrainian

У межах програмної системи для управління вентиляційними системами реалізовано окреме AI-розширення для прогнозування параметрів мікроклімату. Для демонстрації роботи системи використовується сервіс `Device Telemetry Simulator`, який імітує роботу датчиків і вентиляційного обладнання. Він не створює окремий тип згенерованих даних у доменній моделі, а публікує повідомлення у той самий MQTT-формат, який використовується фізичними сенсорами.

Сервіс симуляції створює демонстраційні приміщення, сенсори та вентиляційні пристрої, після чого передає значення CO2, температури та вологості через брокер EMQX. `Telemetry Ingestion Service` приймає ці повідомлення, виконує валідацію, визначає сенсор за серійним номером і зберігає історію у стандартній таблиці `sensor_data`. Зайнятість приміщення використовується тільки як внутрішній параметр сценарію симуляції, а не як телеметрія датчика. Стан припливної та витяжної вентиляції відображається у звичайній історії `device_data`.

`AI Training Job` формує часовий датасет зі стандартних таблиць системи та, за наявності, з нормалізованих відкритих наборів даних. Зовнішні записи не додаються до таблиць телеметрії застосунку, а використовуються лише як додаткове джерело для навчання. Модель реалізована за допомогою scikit-learn як `MultiOutputRegressor` над `HistGradientBoostingRegressor` і прогнозує CO2, температуру та вологість на горизонтах 10, 20 і 30 хвилин. Після навчання зберігаються файл `model.joblib` і метрики якості моделі.

Окремий `AI Prediction Service` на FastAPI надає REST API для прогнозування, симуляції альтернативних режимів вентиляції та формування рекомендацій. AI-модуль не має права напряму керувати вентиляційними пристроями та не публікує команди у MQTT. Він лише повертає прогноз або рекомендацію. Після підтвердження рекомендації backend фіксує команду у стандартній історії `device_data`, змінює статус рекомендації на `used` і публікує MQTT-команду для відповідного приміщення.

У Kubernetes AI-розширення розгортається як набір незалежних компонентів: Deployment для симулятора пристроїв, Deployment і Service для prediction-сервісу, CronJob для періодичного навчання моделі, ConfigMap для налаштувань, Secret для паролів PostgreSQL/MQTT, PVC для збереження моделі та HPA для масштабування prediction-сервісу.

Важливо, що демонстраційні вимірювання формуються програмно, але всередині системи обробляються як звичайна телеметрія пристроїв. Такий підхід використовується для перевірки архітектурного та алгоритмічного рішення. Отримані метрики не є доказом точності для реального об'єкта. Для промислового впровадження модель необхідно донавчити та перевірити на фактичній телеметрії конкретних приміщень і вентиляційного обладнання.
