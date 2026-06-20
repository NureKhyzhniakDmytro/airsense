#!/usr/bin/env bash
set -euo pipefail

CONTEXT="${AIRSENSE_KUBE_CONTEXT:-airsense}"
NAMESPACE="${AIRSENSE_NAMESPACE:-airsense}"
PUBLIC_URL="${AIRSENSE_PUBLIC_URL:-https://airsense.yooud.org}"

echo "Context: ${CONTEXT}"
echo "Namespace: ${NAMESPACE}"
echo

kubectl --context "$CONTEXT" -n "$NAMESPACE" get pods \
  -o custom-columns='NAME:.metadata.name,READY:.status.containerStatuses[*].ready,STATUS:.status.phase,RESTARTS:.status.containerStatuses[*].restartCount,AGE:.metadata.creationTimestamp'

echo
kubectl --context "$CONTEXT" -n "$NAMESPACE" get deploy,statefulset,svc --no-headers 2>/dev/null || true

echo
if curl -fsS "${PUBLIC_URL%/}/api/healthz" >/tmp/airsense-healthz.json 2>/tmp/airsense-healthz.err; then
  printf 'Public API: '
  cat /tmp/airsense-healthz.json
  echo
else
  printf 'Public API: unavailable (%s)\n' "$(cat /tmp/airsense-healthz.err)"
fi
