# AirSense Kubernetes deployment

This directory contains a local Minikube deployment for the service-oriented AirSense model.

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

The deployment script uses a dedicated Minikube profile named `airsense` by default, builds the backend image inside the profile Docker daemon, applies `k8s/base`, restarts the API-derived workloads, waits for rollouts, and runs smoke tests for DNS, TCP service access, and API health.

```bash
./scripts/deploy-minikube.sh
```

Useful overrides:

```bash
MINIKUBE_PROFILE=airsense K8S_NAMESPACE=airsense KUSTOMIZE_DIR=k8s/base ./scripts/deploy-minikube.sh
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
