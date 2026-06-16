#!/usr/bin/env bash
set -euo pipefail

PROFILE="${MINIKUBE_PROFILE:-airsense}"
NAMESPACE="${INGRESS_NAMESPACE:-ingress-nginx}"
SERVICE="${INGRESS_SERVICE:-svc/ingress-nginx-controller}"
BIND_ADDRESS="${AIRSENSE_INGRESS_BIND_ADDRESS:-10.10.0.42}"
BIND_PORT="${AIRSENSE_INGRESS_BIND_PORT:-18080}"
TARGET_PORT="${AIRSENSE_INGRESS_TARGET_PORT:-80}"
LOG_FILE="${AIRSENSE_INGRESS_LOG_FILE:-/tmp/airsense-ingress-port-forward.log}"
PID_FILE="${AIRSENSE_INGRESS_PID_FILE:-/tmp/airsense-ingress-port-forward.pid}"

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
