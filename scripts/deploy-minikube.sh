#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE="${MINIKUBE_PROFILE:-airsense}"
NAMESPACE="${K8S_NAMESPACE:-airsense}"
KUSTOMIZE_DIR="${KUSTOMIZE_DIR:-k8s/base}"
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


apply_local_secret() {
  kubectl --context "$PROFILE" apply -f "${KUSTOMIZE_DIR}/namespace.yaml"
  kubectl --context "$PROFILE" -n "$NAMESPACE" create secret generic airsense-secret \
    --from-literal=postgres-user="$POSTGRES_USER" \
    --from-literal=postgres-password="$POSTGRES_PASSWORD" \
    --from-literal=postgres-database="$POSTGRES_DATABASE" \
    --from-literal=postgres-connection-string="$POSTGRES_CONNECTION_STRING" \
    --from-literal=mqtt-api-password="$MQTT_API_PASSWORD" \
    --from-literal=firebase-project-name="$FIREBASE_PROJECT_NAME" \
    --from-literal=firebase-credentials-file-location="$FIREBASE_CREDENTIALS_FILE_LOCATION" \
    --dry-run=client \
    -o yaml | kubectl --context "$PROFILE" apply -f -
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

apply_local_secret

eval "$(minikube -p "$PROFILE" docker-env)"
docker build -f api/Airsense.API/Dockerfile --target final -t airsense-api:local api
kubectl --context "$PROFILE" apply -k "$KUSTOMIZE_DIR"

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
