# Indoor Air Quality Research Notes

This note records the external basis used for demo telemetry generation and prediction behavior. It is not a certification claim. The demo model is intended to produce plausible telemetry for system demonstration and model-training experiments; a real deployment still requires calibration against measured room volume, airflows, equipment loads, sensor placement, and outdoor conditions.

## Reference Points

- ASHRAE Standards 62.1 and 62.2 are the main ventilation and acceptable indoor air quality standards. They specify minimum ventilation rates and related measures, not a universal indoor CO2 pass/fail limit.
  Source: https://www.ashrae.org/technical-resources/bookstore/standards-62-1-62-2

- ASHRAE's 2025 CO2 guidance states that indoor CO2 is useful for understanding ventilation, but it is not a complete IAQ indicator. A single threshold such as 1000 ppm should not be treated as a universal ASHRAE IAQ requirement.
  Source: https://www.ashrae.org/file%20library/about/government%20affairs/public%20policy%20resources/briefs/indoor-carbon-dioxide--ventilation-and-indoor-air-quality.pdf
  Source: https://www.ashrae.org/file%20library/about/position%20documents/pd-on-indoor-carbon-dioxide-english.pdf

- NIST Technical Note 2213 describes a CO2 ventilation metric based on room-specific conditions: occupant count, occupant CO2 generation, outdoor CO2, ventilation rate, and time since occupancy started. It also uses a single-zone mass-balance model as the simplified engineering basis.
  Source: https://nvlpubs.nist.gov/nistpubs/TechnicalNotes/NIST.TN.2213.pdf

- OSHA's IAQ guidance treats CO2 as a rough ventilation indicator and lists a common comfort check range of 68-78 F (about 20-26 C) and 30-60% relative humidity for offices and commercial spaces.
  Source: https://www.osha.gov/sites/default/files/publications/3430INDOOR-AIR-QUALITY-SM.pdf

- NIOSH/OSHA occupational exposure limits for CO2 are much higher than comfort/ventilation indicators: 5000 ppm time-weighted average, with a NIOSH short-term exposure limit of 30000 ppm. These are safety exposure limits, not comfort targets.
  Source: https://www.cdc.gov/niosh/npg/npgd0103.html

- EPA recommends indoor humidity around 30-50% for general IAQ and mold control. EPA and CDC/NIOSH describe ventilation as a way to dilute or remove indoor pollutants and reduce airborne exposure.
  Source: https://www.epa.gov/indoor-air-quality-iaq/care-your-air-guide-indoor-air-quality
  Source: https://www.epa.gov/indoor-air-quality-iaq/ventilation-and-respiratory-viruses
  Source: https://www.cdc.gov/niosh/ventilation/about/index.html

- Peer-reviewed literature supports using CO2 as a tracer for ventilation and rebreathed-air exposure, with important limitations. Useful references for the diploma report include Rudnick and Milton (2003), Batterman (2017), Persily (1997/2022), and Morawska et al. (2021).
  Source: https://doi.org/10.1034/j.1600-0668.2003.00189.x
  Source: https://pmc.ncbi.nlm.nih.gov/articles/PMC5334699/
  Source: https://www.osti.gov/biblio/349956
  Source: https://pubmed.ncbi.nlm.nih.gov/33601136/

## Expected Behavior For The Demo System

- CO2 should rise with occupancy/load and fall with effective ventilation. The fall should depend on the excess above outdoor background, not on a fixed ppm subtraction independent of the current concentration.
- Higher supply/exhaust airflow should dilute CO2 and reduce humidity. Supply airflow should have stronger cooling/drying influence than exhaust in the simplified model.
- Heat-emitting equipment should raise local temperature, and local sensor values should vary based on sensor position relative to equipment and airflow.
- Humidity should usually stay in the 30-60% band. Values outside that band are acceptable only as scenario/anomaly output, not as a normal target.
- Temperature in office-like rooms should generally remain near 20-26 C. Industrial demo rooms may temporarily run outside that range because equipment heat and ventilation failures are part of the training scenario.

## AirSense Implementation Check

Current implementation matches the expected behavior at the simulation-model level:

- `services/device-telemetry-simulator/app/simulator.py` reads each room layout and binds sensor coordinates, ventilation device role/rotation, and equipment heat sources.
- Room CO2 follows a simplified mass-balance-like direction: occupancy adds CO2, and ventilation removes a fraction of the excess over the outdoor baseline.
- Sensor readings include local heat, local supply/exhaust influence, stable per-sensor calibration offset, and measurement noise.
- `device_data` stores separate supply and exhaust values, so the AI training query can learn separate `supply_ventilation_power` and `exhaust_ventilation_power` features.
- `services/ai-prediction-service/app/model_store.py` uses the same qualitative fallback contract: CO2 decays toward outdoor background with effective ventilation instead of using an unconditional fixed ppm drop.

Live demo snapshot from June 20, 2026 after the simulator update showed plausible short-window ranges:

- CO2: about 585-1068 ppm across the five demo rooms in a three-minute window.
- Relative humidity: about 34-46% in the same window.
- Temperature: about 18-23 C in the same window. The lower values appeared in a demo scenario and should be interpreted as industrial/night or over-ventilation behavior rather than office comfort.
- Supply and exhaust device histories were present and distinct per room.

## Limits And Technical Debt

- The simulator is not a CFD solver. It uses directional gaussian airflow fields and local heat kernels to create plausible spatial gradients.
- CO2 is not treated as a complete IAQ score. The system should not state that CO2 alone proves indoor air is safe.
- The current model does not simulate PM2.5, VOC, CO, NO2, formaldehyde, filtration efficiency, outdoor pollutant ingress, or real air-change rates.
- For real deployment, each environment needs measured room volume, supply/exhaust flow rates, outdoor CO2 baseline, ventilation schedule, sensor calibration metadata, and historical telemetry from the actual facility.
