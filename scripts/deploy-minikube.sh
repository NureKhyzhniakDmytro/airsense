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
FRONTEND_API_INTERNAL_BASE_URL="${FRONTEND_API_INTERNAL_BASE_URL:-http://airsense-api:8080}"
FRONTEND_PUBLIC_API_BASE_URL="${FRONTEND_PUBLIC_API_BASE_URL:-/api}"
FRONTEND_FIREBASE_API_KEY="${FRONTEND_FIREBASE_API_KEY:-}"
FRONTEND_FIREBASE_AUTH_DOMAIN="${FRONTEND_FIREBASE_AUTH_DOMAIN:-}"
FRONTEND_FIREBASE_PROJECT_ID="${FRONTEND_FIREBASE_PROJECT_ID:-}"
FRONTEND_FIREBASE_STORAGE_BUCKET="${FRONTEND_FIREBASE_STORAGE_BUCKET:-}"
FRONTEND_FIREBASE_MESSAGING_SENDER_ID="${FRONTEND_FIREBASE_MESSAGING_SENDER_ID:-}"
FRONTEND_FIREBASE_APP_ID="${FRONTEND_FIREBASE_APP_ID:-}"
FRONTEND_FIREBASE_MEASUREMENT_ID="${FRONTEND_FIREBASE_MEASUREMENT_ID:-}"
FRONTEND_FIREBASE_VAPID_KEY="${FRONTEND_FIREBASE_VAPID_KEY:-}"
ENABLE_MINIKUBE_INGRESS="${ENABLE_MINIKUBE_INGRESS:-true}"
FRONTEND_INGRESS_ENABLED="${FRONTEND_INGRESS_ENABLED:-true}"
FRONTEND_INGRESS_CLASS_NAME="${FRONTEND_INGRESS_CLASS_NAME:-nginx}"
FRONTEND_INGRESS_HOST="${FRONTEND_INGRESS_HOST:-}"
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


configure_minikube_ingress() {
  if ! kubectl --context "$PROFILE" -n ingress-nginx get deployment/ingress-nginx-controller >/dev/null 2>&1; then
    minikube -p "$PROFILE" addons enable ingress || true
  fi

  kubectl --context "$PROFILE" -n ingress-nginx patch deployment ingress-nginx-controller     --type=json     -p='[{"op":"remove","path":"/spec/template/spec/containers/0/ports/0/hostPort"},{"op":"remove","path":"/spec/template/spec/containers/0/ports/1/hostPort"}]'     >/dev/null 2>&1 || true

  wait_rollout deployment/ingress-nginx-controller ingress-nginx 300s
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
    service/airsense-web
    deployment/airsense-web
    ingress/airsense-web
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
    --set-string secret.firebaseCredentialsFileLocation="$FIREBASE_CREDENTIALS_FILE_LOCATION" \
    --set-string frontend.apiInternalBaseUrl="$FRONTEND_API_INTERNAL_BASE_URL" \
    --set-string frontend.publicApiBaseUrl="$FRONTEND_PUBLIC_API_BASE_URL" \
    --set-string frontend.firebase.apiKey="$FRONTEND_FIREBASE_API_KEY" \
    --set-string frontend.firebase.authDomain="$FRONTEND_FIREBASE_AUTH_DOMAIN" \
    --set-string frontend.firebase.projectId="$FRONTEND_FIREBASE_PROJECT_ID" \
    --set-string frontend.firebase.storageBucket="$FRONTEND_FIREBASE_STORAGE_BUCKET" \
    --set-string frontend.firebase.messagingSenderId="$FRONTEND_FIREBASE_MESSAGING_SENDER_ID" \
    --set-string frontend.firebase.appId="$FRONTEND_FIREBASE_APP_ID" \
    --set-string frontend.firebase.measurementId="$FRONTEND_FIREBASE_MEASUREMENT_ID" \
    --set-string frontend.firebase.vapidKey="$FRONTEND_FIREBASE_VAPID_KEY" \
    --set-string frontend.ingress.enabled="$FRONTEND_INGRESS_ENABLED" \
    --set-string frontend.ingress.className="$FRONTEND_INGRESS_CLASS_NAME" \
    --set-string frontend.ingress.host="$FRONTEND_INGRESS_HOST"
}

run_smoke_tests() {
  echo "Running Kubernetes smoke tests..."

  local emqx_host="emqx.${NAMESPACE}.svc.cluster.local"
  local postgres_host="postgres.${NAMESPACE}.svc.cluster.local"
  local redis_host="redis.${NAMESPACE}.svc.cluster.local"
  local api_url="http://airsense-api.${NAMESPACE}.svc.cluster.local:8080/healthz"
  local web_url="http://airsense-web.${NAMESPACE}.svc.cluster.local:3000/login"
  local web_dashboard_url="http://airsense-web.${NAMESPACE}.svc.cluster.local:3000/dashboard"
  local web_api_proxy_url="http://airsense-web.${NAMESPACE}.svc.cluster.local:3000/api/healthz"

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

  local web_html
  web_html="$(run_smoke_pod smoke-web wget -qO- "${web_url}")"
  if [[ "$web_html" != *"Login to your account"* ]]; then
    echo "Web SSR smoke check failed" >&2
    exit 1
  fi

  local dashboard_headers
  dashboard_headers="$(run_smoke_pod smoke-web-redirect wget -S --spider "${web_dashboard_url}" 2>&1 || true)"
  if [[ "$dashboard_headers" != *"HTTP/1.1 302 Found"* || "$dashboard_headers" != *"location: /login"* ]]; then
    echo "Protected route redirect smoke check failed" >&2
    echo "$dashboard_headers" >&2
    exit 1
  fi

  local proxy_health
  proxy_health="$(run_smoke_pod smoke-web-api wget -qO- "${web_api_proxy_url}")"
  if [[ "$proxy_health" != *'"status":"ok"'* ]]; then
    echo "Web API proxy smoke check failed" >&2
    echo "$proxy_health" >&2
    exit 1
  fi

  if [[ "$FRONTEND_INGRESS_ENABLED" == "true" ]]; then
    local ingress_ip
    ingress_ip="$(minikube -p "$PROFILE" ip)"
    local ingress_http_port
    ingress_http_port="$(kubectl --context "$PROFILE" -n ingress-nginx get svc ingress-nginx-controller -o jsonpath='{.spec.ports[0].nodePort}')"
    local ingress_base_url="http://${ingress_ip}:${ingress_http_port}"
    local ingress_curl_args=()
    if [[ -n "$FRONTEND_INGRESS_HOST" ]]; then
      ingress_base_url="http://${FRONTEND_INGRESS_HOST}:${ingress_http_port}"
      ingress_curl_args+=(--resolve "${FRONTEND_INGRESS_HOST}:${ingress_http_port}:${ingress_ip}")
    fi

    local ingress_html
    ingress_html="$(curl -fsS "${ingress_curl_args[@]}" "${ingress_base_url}/login")"
    if [[ "$ingress_html" != *"Login to your account"* ]]; then
      echo "Ingress SSR smoke check failed" >&2
      exit 1
    fi

    local ingress_dashboard_headers
    ingress_dashboard_headers="$(curl -sS -D - -o /dev/null "${ingress_curl_args[@]}" "${ingress_base_url}/dashboard")"
    if [[ "$ingress_dashboard_headers" != *"302 Found"* || "$ingress_dashboard_headers" != *"location: /login"* ]]; then
      echo "Ingress protected route redirect smoke check failed" >&2
      echo "$ingress_dashboard_headers" >&2
      exit 1
    fi

    local ingress_proxy_health
    ingress_proxy_health="$(curl -fsS "${ingress_curl_args[@]}" "${ingress_base_url}/api/healthz")"
    if [[ "$ingress_proxy_health" != *'"status":"ok"'* ]]; then
      echo "Ingress API proxy smoke check failed" >&2
      echo "$ingress_proxy_health" >&2
      exit 1
    fi
  fi
}

minikube -p "$PROFILE" status >/dev/null 2>&1 || minikube -p "$PROFILE" start --driver=docker --cni=false

if [[ "$ENABLE_MINIKUBE_INGRESS" == "true" ]]; then
  configure_minikube_ingress
fi

adopt_existing_resources

eval "$(minikube -p "$PROFILE" docker-env)"
docker build -f api/Airsense.API/Dockerfile --target final -t airsense-api:local api
docker build -f web/Dockerfile -t airsense-web:local web
helm_deploy

for deployment in airsense-api telemetry-ingestion automation notification airsense-web; do
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
wait_rollout deployment/airsense-web

run_smoke_tests
kubectl --context "$PROFILE" -n "$NAMESPACE" get pods,svc,ingress
