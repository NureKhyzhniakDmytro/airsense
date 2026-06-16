#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE="${MINIKUBE_PROFILE:-airsense}"
NAMESPACE="${K8S_NAMESPACE:-airsense}"
RELEASE_NAME="${HELM_RELEASE_NAME:-airsense}"
CHART_DIR="${HELM_CHART_DIR:-charts/airsense}"
POSTGRES_USER="${POSTGRES_USER:-postgres}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-airsense1234}"
POSTGRES_DATABASE="${POSTGRES_DATABASE:-airsensedb}"
POSTGRES_CONNECTION_STRING="${POSTGRES_CONNECTION_STRING:-Host=postgres;Port=5432;Database=${POSTGRES_DATABASE};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}}"
MQTT_API_PASSWORD="${MQTT_API_PASSWORD:-airsense-api-secret}"
FIREBASE_PROJECT_NAME="${FIREBASE_PROJECT_NAME:-}"
FIREBASE_CREDENTIALS_FILE_LOCATION="${FIREBASE_CREDENTIALS_FILE_LOCATION:-}"
cd "$ROOT_DIR"

wait_rollout() {
  local resource="$1"
  local namespace="${2:-$NAMESPACE}"
  local timeout="${3:-180s}"
  kubectl --context "$PROFILE" -n "$namespace" rollout status "$resource" --timeout="$timeout"
}

run_smoke_pod() {
  local name="$1"
  shift
  kubectl --context "$PROFILE" -n "$NAMESPACE" delete pod "$name" --ignore-not-found >/dev/null 2>&1 || true
  kubectl --context "$PROFILE" -n "$NAMESPACE" run "$name" \
    --image=busybox:1.36 \
    --restart=Never \
    --rm \
    -i \
    --quiet \
    -- "$@"
}

adopt_existing_resources() {
  kubectl --context "$PROFILE" create namespace "$NAMESPACE" --dry-run=client -o yaml \
    | kubectl --context "$PROFILE" apply -f -

  local resources=(
    secret/airsense-secret
    configmap/emqx-config
    configmap/postgres-initdb
    service/airsense-api
    service/emqx
    service/postgres
    service/redis
    deployment/airsense-api
    deployment/telemetry-ingestion
    deployment/automation
    deployment/notification
    deployment/emqx
    deployment/redis
    statefulset/postgres
  )

  for resource in "${resources[@]}"; do
    if kubectl --context "$PROFILE" -n "$NAMESPACE" get "$resource" >/dev/null 2>&1; then
      kubectl --context "$PROFILE" -n "$NAMESPACE" annotate "$resource" \
        meta.helm.sh/release-name="$RELEASE_NAME" \
        meta.helm.sh/release-namespace="$NAMESPACE" \
        --overwrite >/dev/null
      kubectl --context "$PROFILE" -n "$NAMESPACE" label "$resource" \
        app.kubernetes.io/managed-by=Helm \
        --overwrite >/dev/null
    fi
  done
}

helm_deploy() {
  helm upgrade --install "$RELEASE_NAME" "$CHART_DIR" \
    --kube-context "$PROFILE" \
    --namespace "$NAMESPACE" \
    --create-namespace \
    --take-ownership \
    --server-side=false \
    --set-string secret.postgresUser="$POSTGRES_USER" \
    --set-string secret.postgresPassword="$POSTGRES_PASSWORD" \
    --set-string secret.postgresDatabase="$POSTGRES_DATABASE" \
    --set-string secret.postgresConnectionString="$POSTGRES_CONNECTION_STRING" \
    --set-string secret.mqttApiPassword="$MQTT_API_PASSWORD" \
    --set-string secret.firebaseProjectName="$FIREBASE_PROJECT_NAME" \
    --set-string secret.firebaseCredentialsFileLocation="$FIREBASE_CREDENTIALS_FILE_LOCATION"
}

run_smoke_tests() {
  echo "Running Kubernetes smoke tests..."

  local emqx_host="emqx.${NAMESPACE}.svc.cluster.local"
  local postgres_host="postgres.${NAMESPACE}.svc.cluster.local"
  local redis_host="redis.${NAMESPACE}.svc.cluster.local"
  local api_url="http://airsense-api.${NAMESPACE}.svc.cluster.local:8080/healthz"

  run_smoke_pod smoke-dns sh -c \
    "nslookup ${emqx_host} && nslookup ${postgres_host} && nslookup ${redis_host}"

  run_smoke_pod smoke-tcp sh -c \
    "nc -z -w 3 ${emqx_host} 1883 && nc -z -w 3 ${postgres_host} 5432 && nc -z -w 3 ${redis_host} 6379"

  local health
  health="$(run_smoke_pod smoke-api wget -qO- "${api_url}")"
  echo "$health"
  if [[ "$health" != *'"status":"ok"'* ]]; then
    echo "API health check failed" >&2
    exit 1
  fi
}

minikube -p "$PROFILE" status >/dev/null 2>&1 || minikube -p "$PROFILE" start --driver=docker --cni=false

adopt_existing_resources

eval "$(minikube -p "$PROFILE" docker-env)"
docker build -f api/Airsense.API/Dockerfile --target final -t airsense-api:local api
helm_deploy

for deployment in airsense-api telemetry-ingestion automation notification; do
  kubectl --context "$PROFILE" -n "$NAMESPACE" rollout restart "deployment/${deployment}"
done

wait_rollout deployment/coredns kube-system
wait_rollout daemonset/kube-proxy kube-system
wait_rollout statefulset/postgres
wait_rollout deployment/redis
wait_rollout deployment/emqx
wait_rollout deployment/airsense-api
wait_rollout deployment/telemetry-ingestion
wait_rollout deployment/automation
wait_rollout deployment/notification

run_smoke_tests
kubectl --context "$PROFILE" -n "$NAMESPACE" get pods,svc
