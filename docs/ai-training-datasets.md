# AI Training Datasets

This document records the datasets used to train the AirSense AI prediction service on June 21, 2026.

## Normalized AirSense Schema

All external datasets are converted to the same training schema before model fitting:

```text
source
series_id
room_id
timestamp
co2
temperature
humidity
ventilation_power
supply_ventilation_power
exhaust_ventilation_power
```

The training job then adds time features and predicts CO2, temperature and humidity for 10, 20 and 30 minute horizons.

## External Public Datasets

### UCI Occupancy Detection

- Source: https://archive.ics.uci.edu/dataset/357/occupancy+detection
- Downloaded archive: `https://archive.ics.uci.edu/static/public/357/occupancy+detection.zip`
- Normalized file: `services/ai-prediction-service/datasets/normalized/uci_occupancy_detection.csv`
- Rows used: 20,560
- Series used: `datatraining`, `datatest`, `datatest2`
- Relevant source fields: `date`, `Temperature`, `Humidity`, `CO2`
- License noted by UCI: Creative Commons Attribution 4.0 International.

Use in AirSense:

- Temperature, humidity and CO2 are mapped directly.
- Ventilation features are set to `0` because the source dataset does not include HVAC device telemetry.
- The dataset is useful for indoor short-horizon environmental dynamics, but not for learning supply/exhaust ventilation effects.

### UCI Room Occupancy Estimation

- Source: https://archive.ics.uci.edu/dataset/864/room%2Boccupancy%2Bestimation
- Downloaded archive: `https://archive.ics.uci.edu/static/public/864/room+occupancy+estimation.zip`
- Normalized file: `services/ai-prediction-service/datasets/normalized/uci_room_occupancy_estimation.csv`
- Rows used: 10,129
- Series used: `main`
- Relevant source fields: `Date`, `Time`, `S1_Temp` to `S4_Temp`, `S5_CO2`, `Room_Occupancy_Count`
- License noted by UCI: Creative Commons Attribution 4.0 International.

Use in AirSense:

- Temperature is the average of `S1_Temp` to `S4_Temp`.
- CO2 is mapped from `S5_CO2`.
- Humidity is not present in the source dataset, so it is imputed from occupancy, daily cycle and temperature to satisfy the AirSense multi-output contract.
- UCI notes that no HVAC systems were used while this dataset was collected, so ventilation features are set to `0`.
- Humidity values from this dataset should be described as imputed in reports and should not be treated as measured humidity.

## Merged External Dataset

- File: `services/ai-prediction-service/datasets/normalized/airsense_external_training.csv`
- Rows: 30,689
- Included sources:
  - `uci_occupancy_detection`: 20,560 rows
  - `uci_room_occupancy_estimation`: 10,129 rows

This merged CSV is copied into the `airsense-ai-prediction` image and read by the Kubernetes AI training job. External rows are not inserted into `sensor_data` or `device_data`.

Generated CSV and ZIP artifacts are intentionally ignored by Git. On a fresh checkout, regenerate the public dataset files before building the AI image:

```bash
docker build -t airsense-ai-prediction:dataset-tools services/ai-prediction-service
docker run --rm \
  -v "${PWD}/services/ai-prediction-service/datasets:/app/datasets" \
  airsense-ai-prediction:dataset-tools \
  python -m app.external_datasets --datasets-root /app/datasets download-normalize
```

## AirSense Synthetic Datasets

The first demo room was seeded as a month-long industrial production room scenario:

- Period: 2026-05-21 16:00:00 UTC to 2026-06-20 16:00:00 UTC
- Interval: 5 minutes
- Room setup: 3 heat-emitting machines, 3 sensors and 2 ventilation devices
- Seed script: `scripts/seed-industrial-month.py`
- Seed result: 77,769 sensor rows and 17,282 device rows for room 1 at generation time.

Local preserved artifacts:

- Training-view snapshot: `services/ai-prediction-service/datasets/synthetic/airsense_synthetic_snapshot_20260620.csv`
- Raw sensor export: `services/ai-prediction-service/datasets/synthetic/airsense_synthetic_raw_sensor_data_20260620.csv`
- Raw device export: `services/ai-prediction-service/datasets/synthetic/airsense_synthetic_raw_device_data_20260620.csv`

The snapshot is generated from the same room-minute training query used by the model. The raw exports preserve the ordinary application telemetry rows from `sensor_data` and `device_data`.

## Training Result

The active trained model after the latest Kubernetes training run is:

- Model version: `hgb-20260621163014`
- Model type: scikit-learn `MultiOutputRegressor` over `HistGradientBoostingRegressor`
- Raw training rows: 35,805
- AirSense DB rows: 5,116
- External dataset rows: 30,689
- Supervised rows: 35,378
- Train rows: 28,301
- Validation rows: 7,077
- Series: 8

Validation metrics:

- CO2 MAE: 101.8942 ppm
- Temperature MAE: 0.2215 C
- Humidity MAE: 1.0194 %

## Limitations

- Kaggle datasets were not downloaded because no Kaggle credentials were configured in the runtime environment.
- The UCI datasets provide real indoor environmental measurements, but they do not include supply/exhaust ventilation telemetry.
- Ventilation behavior is primarily learned from AirSense synthetic/demo history.
- The model is suitable for demo and diploma analysis, not for certified real-world control without calibration on the actual facility.
