# Technical Debt

## Mobile Client Scope

Mobile is now maintained as a basic read-only companion client for the current backend/web/AI demo iteration.

Closed in the current mobile refresh:

- auth failures are surfaced through ViewModel state;
- logout/navigation placeholders were replaced with concrete callbacks;
- mobile DTOs and Retrofit endpoints now cover the current read-only backend contract for environments, rooms, sensors, devices, history, AI insights and notifications;
- read-only role helpers are covered by mobile unit tests;
- Android debug tests and build are verified locally with a generated Firebase client config.

Remaining gaps:

- mobile does not attempt feature parity with web management flows such as room layout editing, demo topology management, automation editing, or applying AI recommendations;
- local Android builds require `mobile/local.properties` and ignored `mobile/app/google-services.json`; the Firebase client config can be regenerated from the backend service account with `node scripts/generate-mobile-google-services.mjs`;
- the standard Android backup-rules template still needs a product decision before release.

Decision: keep mobile scoped as a viewer until the web/backend demo path is stable.

## PostgreSQL Backup And Restore

The Helm chart now mounts PostgreSQL data through a `PersistentVolumeClaim` by default. A normal Postgres pod restart should keep the database volume attached instead of recreating the seed database from scratch.

Remaining production debt:

- define a backup and restore workflow for PostgreSQL;
- define retention for telemetry history and notification history;
- document disaster recovery steps for a lost Minikube volume or a replaced storage class.

Mitigation in the current demo flow: login re-adds the authenticated user to the demo environment as read-only `user`, and the simulator/migration can rebuild demo topology, telemetry, and room layout bindings if the local database is intentionally reset.
