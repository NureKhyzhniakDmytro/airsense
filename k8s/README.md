# AirSense Kubernetes deployment

AirSense is deployed to Kubernetes through the Helm chart in `charts/airsense`.
The chart describes the service-oriented backend model and its local infrastructure dependencies.

Runtime roles use the same `airsense-api:local` image and are separated by `Airsense__ServiceRole`:

- `api`: REST API and EMQX HTTP auth/ACL callbacks.
- `telemetry-ingestion`: MQTT consumer for sensor telemetry, validation, persistence, and telemetry event publishing.
- `automation`: telemetry event consumer, fan-speed calculation, device command publishing, and critical notification event publishing.
- `notification`: notification event consumer and Firebase Cloud Messaging integration.

Infrastructure:

- PostgreSQL stores domain data and telemetry history.
- EMQX provides MQTT communication with sensors, devices, and internal service events.
- Redis is deployed as a prepared infrastructure component for cached/latest telemetry values and future queue use.

## Local Minikube deploy

The deployment script uses a dedicated Minikube profile named `airsense` by default. It builds the backend image inside the profile Docker daemon, installs or upgrades the Helm release, restarts the API-derived workloads so the local image is refreshed, waits for rollouts, and runs smoke tests for DNS, TCP service access, and API health.

```bash
./scripts/deploy-minikube.sh
```

Useful overrides:

```bash
MINIKUBE_PROFILE=airsense \
K8S_NAMESPACE=airsense \
HELM_RELEASE_NAME=airsense \
HELM_CHART_DIR=charts/airsense \
./scripts/deploy-minikube.sh
```

Manual Helm rendering and installation:

```bash
helm lint charts/airsense
helm template airsense charts/airsense --namespace airsense
helm upgrade --install airsense charts/airsense --namespace airsense --create-namespace
```

Manual health check:

```bash
kubectl --context airsense -n airsense port-forward svc/airsense-api 8080:8080
curl http://localhost:8080/healthz
```

EMQX dashboard can be exposed with:

```bash
kubectl --context airsense -n airsense port-forward svc/emqx 18083:18083
```

## Local Secret values

The chart template creates `airsense-secret` from `.Values.secret`. Default values in `charts/airsense/values.yaml` are suitable only for local Minikube. For private values use environment variables with `scripts/deploy-minikube.sh` or pass a local values file to Helm.

```bash
POSTGRES_USER=postgres \
POSTGRES_PASSWORD=airsense1234 \
POSTGRES_DATABASE=airsensedb \
MQTT_API_PASSWORD=airsense-api-secret \
FIREBASE_PROJECT_NAME="" \
FIREBASE_CREDENTIALS_FILE_LOCATION="" \
./scripts/deploy-minikube.sh
```

The script derives `POSTGRES_CONNECTION_STRING` from the PostgreSQL values unless it is explicitly provided.

## Workload health and resources

The Helm chart defines startup, readiness, and liveness probes for the backend roles and infrastructure workloads. Resource requests and limits are configurable in `charts/airsense/values.yaml` for API, telemetry ingestion, automation, notification, PostgreSQL, Redis, and EMQX.

## Nested Incus/LXC note

When Minikube runs inside an Incus system container, the container must allow nested Docker/Kubernetes networking. The AirSense Minikube profile requires CNI and kube-proxy to program iptables rules. The following kernel modules were required on the host/container configuration during local verification:

```text
overlay, br_netfilter, ip_tables, iptable_nat, iptable_filter, iptable_mangle,
nf_tables, nft_compat, xt_comment, xt_mark, xt_conntrack, xt_tcpudp,
xt_addrtype, xt_nat, xt_set, xt_MASQUERADE, ipt_REJECT, nf_reject_ipv4,
ip6_tables, ip6table_filter, ip6table_mangle, ip6table_nat, ip6t_REJECT,
nf_reject_ipv6, xt_nfacct
```

The key symptoms of a missing module are pods stuck in `ContainerCreating`, CoreDNS not becoming ready, or kube-proxy logs containing `iptables-restore` errors for `comment`, `mark`, `MARK`, or `REJECT` extensions.
