# Technical Debt

## Mobile Client Scope

Mobile is intentionally deferred for the current backend/web/AI demo iteration.

Known gaps:

- `mobile/app/src/main/java/org/yooud/airsense/auth/AuthViewModel.kt` still has TODO-only error handling for auth failures.
- `mobile/app/src/main/java/org/yooud/airsense/ui/HomeScreen.kt` still has a TODO placeholder for logout/navigation.
- The mobile API surface is behind the web/backend contract and does not expose the AI/demo management flows.
- Mobile read-only role behavior has not been audited against the unified environment contract.

Decision: keep this as documented debt until the web/backend demo path is stable.

## PostgreSQL Backup And Restore

The Helm chart now mounts PostgreSQL data through a `PersistentVolumeClaim` by default. A normal Postgres pod restart should keep the database volume attached instead of recreating the seed database from scratch.

Remaining production debt:

- define a backup and restore workflow for PostgreSQL;
- define retention for telemetry history and notification history;
- document disaster recovery steps for a lost Minikube volume or a replaced storage class.

Mitigation in the current demo flow: login re-adds the authenticated user to the demo environment as read-only `user`, and the simulator/migration can rebuild demo topology, telemetry, and room layout bindings if the local database is intentionally reset.
