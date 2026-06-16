# AirSense Helm chart

This chart deploys the service-oriented AirSense backend model to Kubernetes:

- API Service
- Telemetry Ingestion Service
- Automation Service
- Notification Service
- PostgreSQL
- Redis
- EMQX
- Nuxt SSR Web

The backend roles use one Docker image and are separated by `Airsense__ServiceRole`. The web client runs as a Nuxt/Nitro SSR service and proxies browser `/api` requests to the backend service inside the cluster.

## Local install

The repository deployment script builds the backend and frontend images in the Minikube Docker daemon and installs the chart. Local secrets and Firebase settings are read from `.env` when it exists. Kubernetes, Helm, ingress, and frontend API wiring are fixed for the local Minikube stand:

```bash
cp .env.example .env
# edit .env
./scripts/deploy-minikube.sh
```

Manual Helm install example:

```bash
helm upgrade --install airsense charts/airsense \
  --namespace airsense \
  --create-namespace \
  --set-string secret.postgresPassword=airsense1234 \
  --set-string secret.mqttApiPassword=airsense-api-secret
```

Use `values.yaml` for image names, service ports, probes, resource requests/limits, and local Secret values.


## Local ingress

Ingress is enabled by default for the Nuxt SSR service. The local values use a hostless rule so it can be opened by IP address.

```bash
INGRESS_PORT=$(kubectl --context airsense -n ingress-nginx get svc ingress-nginx-controller -o jsonpath='{.spec.ports[0].nodePort}')
curl http://$(minikube -p airsense ip):${INGRESS_PORT}/login
```

The Nuxt service handles `/api/*` through a server-side proxy to `airsense-api` inside the cluster.

### Expose ingress on the dev host

To open the local Minikube ingress from another machine over the dev host address, run:

```bash
./scripts/expose-minikube-ingress.sh
```

The default external URL is:

```text
http://10.10.0.42:18080/
```

This uses `kubectl port-forward` to expose `ingress-nginx-controller:80`; no additional Docker proxy is used.
