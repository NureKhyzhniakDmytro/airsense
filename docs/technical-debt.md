# Technical Debt

## Mobile Client Scope

Mobile is intentionally deferred for the current backend/web/AI demo iteration.

Known gaps:

- `mobile/app/src/main/java/org/yooud/airsense/auth/AuthViewModel.kt` still has TODO-only error handling for auth failures.
- `mobile/app/src/main/java/org/yooud/airsense/ui/HomeScreen.kt` still has a TODO placeholder for logout/navigation.
- The mobile API surface is behind the web/backend contract and does not expose the AI/demo management flows.
- Mobile read-only role behavior has not been audited against the unified environment contract.

Decision: keep this as documented debt until the web/backend demo path is stable.

## Local PostgreSQL Persistence

The Helm chart currently mounts PostgreSQL data on `emptyDir`. A Postgres pod restart recreates the seed database, which is acceptable for disposable local demos but drops runtime-created Firebase users, memberships, and telemetry history.

Mitigation in the current demo flow: login now re-adds the authenticated user to the demo environment as read-only `user`, and the simulator/migration rebuild demo topology, telemetry, and room layout bindings.

Debt: replace `emptyDir` with a PVC-backed volume before treating the Minikube deployment as stateful.
