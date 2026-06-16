# AirSense

AirSense is a smart indoor air-quality monitoring and automation system. The repository is now organized as a single monorepo for the software components used in the diploma project.

## Repository Structure

- `api/` - ASP.NET Core backend, REST API, MQTT integration, service-role workers, PostgreSQL schema.
- `web/` - Nuxt 3 web client application.
- `mobile/` - mobile client application.
- `charts/airsense/` - Helm chart for local Kubernetes/Minikube deployment.
- `scripts/` - project automation scripts.
- `docs/` - architecture and infrastructure notes.

The IoT firmware code is intentionally not included in this monorepo. In the diploma scope it is treated as an external device-side component that communicates with the platform through MQTT.

## Web Client

The web client in `web/` is a Nuxt 3 SPA. It uses file-based routing, a dashboard layout, Pinia stores, PrimeVue components, Firebase authentication, and Axios integration with the backend API.

```bash
cd web
npm install
npm run dev
```

## Local Kubernetes Deployment

A dedicated Minikube profile named `airsense` is used by default:

```bash
./scripts/deploy-minikube.sh
```

The script builds the backend image, installs or upgrades the `airsense` Helm release from `charts/airsense`, restarts API-derived workloads, waits for rollouts, and runs smoke tests for Kubernetes DNS, TCP service access, and API health.

Manual API access:

```bash
kubectl --context airsense -n airsense port-forward svc/airsense-api 8080:8080
curl http://localhost:8080/healthz
```

More details are documented in `docs/infrastructure.md` and `k8s/README.md`.
