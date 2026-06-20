# AirSense Infrastructure Notes

## Monorepo Layout

The project was converted from a repository that referenced separate Git submodules into a single monorepo. The backend, web client, and mobile client are now stored as normal directories under the root repository:

- `api/`
- `web/`
- `mobile/`

The `iot/` submodule was removed from the monorepo. Device firmware is outside the current repository boundary and is represented in the platform through MQTT topics and EMQX broker integration.

## Backend Service Roles

The backend image is shared by several runtime roles. The active role is selected through the `Airsense__ServiceRole` configuration value.

- `api` exposes REST controllers and EMQX HTTP authentication/ACL callbacks.
- `telemetry-ingestion` subscribes to sensor telemetry topics, validates data, persists telemetry, and publishes an internal telemetry event.
- `automation` consumes internal telemetry events, evaluates room/device automation, and publishes commands or notification events.
- `notification` consumes notification events and delegates delivery to Firebase Cloud Messaging when configured.

This keeps the deployment model service-oriented while avoiding separate build artifacts for each backend role.

## Messaging and Storage

- EMQX is the MQTT broker for device messages and internal service events.
- PostgreSQL stores domain data, users, rooms, sensors, devices, settings, and telemetry history.
- Redis is deployed as prepared infrastructure for future caching or queue-related use cases. Current telemetry and command history are persisted in PostgreSQL.
- Firebase Cloud Messaging remains optional in local Kubernetes. When Firebase credentials are not mounted, no-op auth/notification services allow the local stack to start.

Sensor telemetry enters through MQTT topics in the form `sensor/{parameter}` with the sensor serial number in the `serial-number` MQTT user property. Room commands are published to `room/{roomId}` and are also recorded in `device_data`, so polling devices and MQTT subscribers see the same command intent.

## Access Model

Environment membership is split into read and manage capabilities:

- `user` is read-only and can view rooms, layouts, assets, telemetry history, AI forecasts, and members.
- `admin` and `owner` can manage rooms, sensors, devices, automation curves, layouts, and AI recommendation actions.
- `owner` remains required for destructive environment-level actions and role updates.

## Web Client

The web client is implemented as a Nuxt 3 single-page application. Nuxt file-based routing replaces the previous manual Vue Router setup, while the existing dashboard pages, Pinia stores, PrimeVue UI components, Firebase authentication, and Axios API integration are preserved. Public runtime configuration is used for the API base URL and Firebase client settings.

## Kubernetes Deployment

The local Kubernetes deployment is described by the Helm chart in `charts/airsense`. Helm is used as the primary deployment mechanism instead of maintaining separate raw manifests.

The chart includes:

- Deployments for API, telemetry ingestion, automation, notification, EMQX, and Redis.
- Deployments for the AI prediction service and demo telemetry simulator.
- CronJob for AI model training.
- StatefulSet for PostgreSQL.
- ClusterIP services for API, EMQX, PostgreSQL, and Redis.
- ConfigMaps for EMQX configuration and PostgreSQL initialization scripts.
- A templated local Secret for development values, configured through `values.yaml`, environment variables, or a local Helm values file.

The deployment script is `scripts/deploy-minikube.sh`. It performs these steps:

1. Ensures Minikube profile `airsense` is running.
2. Adopts existing Kubernetes resources into Helm ownership during migration from the earlier manifest-based deployment.
3. Builds `airsense-api:local` inside the Minikube Docker daemon.
4. Runs `helm upgrade --install` for release `airsense` from `charts/airsense`.
5. Restarts backend-role deployments so the local image is refreshed.
6. Waits for CoreDNS, kube-proxy, infrastructure, and backend role rollouts.
7. Runs smoke tests for DNS, TCP connectivity, and `/healthz`.

## Kubernetes Hardening

The Helm chart includes startup probes, readiness probes, and liveness probes for API-derived services, PostgreSQL, Redis, and EMQX. Resource requests and limits are defined for each workload to make the Minikube deployment more predictable and closer to a production Kubernetes description.

Local credentials are not committed as a standalone Kubernetes Secret manifest. `charts/airsense/values.yaml` documents the required keys, while `scripts/deploy-minikube.sh` passes local environment values to Helm during installation or upgrade.

## MQTT Resilience

`MqttServiceBase` now keeps background services alive when EMQX or Kubernetes DNS is not immediately available. It uses a reconnect loop with exponential backoff and logs connection failures instead of allowing `BackgroundService` exceptions to stop the ASP.NET host. This avoids CrashLoopBackOff during normal Kubernetes startup ordering.

The reconnect behavior was verified by scaling the EMQX deployment to zero. API and worker pods remained `Running`, logged retry attempts, and reconnected after EMQX was restored.

MQTT and device HTTP inputs are hardened against malformed topics, malformed JSON payloads, and invalid Basic authentication headers. Invalid external messages are ignored or rejected without crashing background services.

## Local Incus/Minikube Requirements

The verified remote development environment runs Minikube inside an Incus/LXC container. Kubernetes CNI and kube-proxy require netfilter modules to be available to the container. Missing modules caused pods to remain in `ContainerCreating`, CoreDNS readiness failures, and kube-proxy `iptables-restore` errors.

The working Incus configuration included these kernel modules:

```text
overlay, br_netfilter, ip_tables, iptable_nat, iptable_filter, iptable_mangle,
nf_tables, nft_compat, xt_comment, xt_mark, xt_conntrack, xt_tcpudp,
xt_addrtype, xt_nat, xt_set, xt_MASQUERADE, ipt_REJECT, nf_reject_ipv4,
ip6_tables, ip6table_filter, ip6table_mangle, ip6table_nat, ip6t_REJECT,
nf_reject_ipv6, xt_nfacct
```

Useful checks after starting Minikube:

```bash
kubectl --context airsense -n kube-system get pods
kubectl --context airsense -n kube-system logs -l k8s-app=kube-proxy --tail=80
kubectl --context airsense -n airsense get pods,svc,endpoints
```

Expected result after deployment:

```text
airsense-api          1/1 Running
telemetry-ingestion   1/1 Running
automation            1/1 Running
notification          1/1 Running
emqx                  1/1 Running
postgres              1/1 Running
redis                 1/1 Running
```
