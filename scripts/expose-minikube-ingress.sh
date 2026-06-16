#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${AIRSENSE_ENV_FILE:-$ROOT_DIR/.env}"
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

PROFILE="airsense"
NAMESPACE="ingress-nginx"
SERVICE="svc/ingress-nginx-controller"
BIND_ADDRESS="10.10.0.42"
BIND_PORT="18080"
TARGET_PORT="80"
LOG_FILE="/tmp/airsense-ingress-port-forward.log"
PID_FILE="/tmp/airsense-ingress-port-forward.pid"

if [[ -f "$PID_FILE" ]]; then
  old_pid="$(cat "$PID_FILE")"
  if [[ -n "$old_pid" ]] && kill -0 "$old_pid" >/dev/null 2>&1; then
    kill "$old_pid" >/dev/null 2>&1 || true
  fi
  rm -f "$PID_FILE"
fi

nohup kubectl --context "$PROFILE" -n "$NAMESPACE" port-forward   --address "$BIND_ADDRESS"   "$SERVICE"   "${BIND_PORT}:${TARGET_PORT}"   >"$LOG_FILE" 2>&1 &

echo $! >"$PID_FILE"
sleep 2

if ! kill -0 "$(cat "$PID_FILE")" >/dev/null 2>&1; then
  cat "$LOG_FILE" >&2
  exit 1
fi

echo "AirSense ingress is available at http://${BIND_ADDRESS}:${BIND_PORT}/"
echo "Logs: ${LOG_FILE}"
