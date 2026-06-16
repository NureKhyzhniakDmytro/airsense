# AirSense Helm chart

This chart deploys the service-oriented AirSense backend model to Kubernetes:

- API Service
- Telemetry Ingestion Service
- Automation Service
- Notification Service
- PostgreSQL
- Redis
- EMQX

The backend roles use one Docker image and are separated by `Airsense__ServiceRole`.

## Local install

The repository deployment script builds the backend image in the Minikube Docker daemon and installs the chart:

```bash
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
