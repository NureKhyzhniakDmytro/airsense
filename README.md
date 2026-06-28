# AirSense

AirSense is a diploma project for a software system that monitors indoor microclimate and manages ventilation systems. The system collects telemetry from sensors, stores historical measurements, shows the current state in a web dashboard, supports ventilation automation, sends notifications, and provides short-horizon AI forecasts and recommendations.

The repository is organized as one monorepo for the software part of the project. IoT firmware is intentionally outside the repository scope. Physical or simulated devices communicate with the platform through the MQTT contract.

## Monorepo structure

- `api/` - ASP.NET Core backend, REST API, Firebase authentication, PostgreSQL schema, MQTT integration, and background service roles.
- `web/` - Nuxt 3 SSR web client with PrimeVue, Pinia, Firebase Auth, and dashboard workflows.
- `mobile/` - Android companion client for read-only monitoring and push notifications.
- `services/device-telemetry-simulator/` - demo telemetry simulator that imitates ordinary sensors and ventilation devices.
- `services/ai-prediction-service/` - FastAPI service for forecasting, scenario simulation, recommendations, and model training.
- `charts/airsense/` - Helm chart used as the source of truth for Kubernetes deployment.
- `scripts/` - deployment, validation, data, and utility scripts.
- `docs/` - architecture, infrastructure, AI, and handoff notes.

## Runtime model

The backend uses one shared image with several roles selected by configuration:

- `api` - REST API, authentication, and EMQX HTTP auth callbacks.
- `telemetry-ingestion` - MQTT telemetry ingestion, validation, and persistence.
- `automation` - ventilation decision logic and MQTT control publication.
- `notification` - Firebase Cloud Messaging notification delivery.

The AI service does not publish MQTT commands directly. It returns forecasts, simulations, recommendations, or AI-mode decisions. Final command recording and MQTT publication remain in the backend and automation layer.

## Demo deployment

The demo stand is deployed at:

<https://airsense.yooud.org>

The current demo runs in Kubernetes on a local Minikube profile named `airsense`. Public access is exposed through the cluster ingress. The deployment is intended for diploma demonstration and development validation, not as a production installation.

The simulator can generate ordinary MQTT telemetry for demo rooms without requiring real IoT hardware. These messages pass through the same ingestion and automation path as device telemetry.

## Local Kubernetes deployment

Prepare configuration first:

```bash
cp .env.example .env
```

Deploy to the Minikube profile:

```bash
./scripts/deploy-minikube.sh
```

Check the cluster state:

```bash
./scripts/kube-brief.sh
kubectl get deploy,sts,pods,svc,ingress -n airsense
```

Manual API health check:

```bash
kubectl --context airsense -n airsense port-forward svc/airsense-api 8080:8080
curl http://localhost:8080/healthz
```

## Development checks

Backend:

```bash
dotnet test api/airsense.sln
```

Web client:

```bash
cd web
npm install
npm test
npm run build
```

Project-level quiet check:

```bash
./scripts/check-quiet.sh
```

## Documentation

Useful project notes are stored in `docs/`, especially:

- `docs/infrastructure.md`
- `docs/ai-demo-extension.md`
- `docs/ai-training-datasets.md`
- `docs/REMOTE_DEVELOPMENT_CONTEXT.md`
