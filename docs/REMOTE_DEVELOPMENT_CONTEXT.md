# AirSense Remote Development Context

This file is the handoff point for continuing development directly on the remote server.

## Workspace

- Remote host alias: `dev`
- Main remote path: `/opt/airsense`
- Git branch: `codex/kubernetes-orchestration`
- Public demo URL: `https://airsense.yooud.org`
- Local/minikube ingress URL used during development: `http://10.10.0.42:18080`
- Kubernetes namespace: `airsense`
- Minikube profile: `airsense`

Do not assume the working tree is clean. There are many intentional uncommitted changes from the current diploma iteration.

## Important Documentation

Repository documentation:

- `/opt/airsense/README.md`
- `/opt/airsense/k8s/README.md`
- `/opt/airsense/charts/airsense/README.md`
- `/opt/airsense/docs/infrastructure.md`
- `/opt/airsense/docs/ai-demo-extension.md`
- `/opt/airsense/docs/archive-context/service_kubernetes_architecture.md`

Diploma source documents copied to the remote server:

- `/opt/airsense/docs/diploma-sources/2026_Б_ПІ_Пр_ПЗПІ_22_10_Хижняк_Д_С_docx_звіт_з_практики.docx`
- `/opt/airsense/docs/diploma-sources/2025_Б_ККП_ПЗПІ-22-10_Хижняк_Д_С.docx`
- `/opt/airsense/docs/diploma-sources/2026_Б_ПІ_Пр_ПЗПІ_22_3_Ткаченко_Є_А_docx.docx`
- `/opt/airsense/docs/diploma-sources/methodology_bachelor_2026.pdf`

## Current Architecture

The project is now a single monorepo, excluding the IoT firmware part. The main parts are:

- `api/` - ASP.NET Core backend.
- `web/` - Nuxt 3 SSR frontend with PrimeVue.
- `mobile/` - mobile client, currently not the active focus.
- `services/device-telemetry-simulator/` - demo device telemetry simulator.
- `services/ai-prediction-service/` - FastAPI prediction service and training job code.
- `charts/airsense/` - Helm chart used as the main Kubernetes deployment description.
- `scripts/deploy-minikube.sh` - local Minikube deployment script.

Backend runtime roles are selected with `Airsense__ServiceRole`:

- `api`
- `telemetry-ingestion`
- `automation`
- `notification`

The backend image is shared by these roles. EMQX is the MQTT broker, PostgreSQL is the primary data store, Redis is prepared infrastructure, and Firebase is used for auth/notifications where configured.

## AI/Demo Data Extension

The current diploma direction includes a synthetic/demo telemetry source, but the system must not classify telemetry as synthetic inside the domain model. The simulator imitates normal devices and publishes ordinary MQTT telemetry.

Implemented/started components:

- Device telemetry simulator service.
- Demo data management backend API.
- Demo data UI page at `/dashboard/demo-data`.
- AI prediction service on FastAPI.
- AI training job/CronJob support.
- Helm resources for simulator, AI service, training job, ConfigMap/Secret/PVC/HPA pieces.

The simulator prepares ordinary environments, rooms, sensors, devices, settings and then produces normal `sensor_data` / `device_data` history. Use `/opt/airsense/docs/ai-demo-extension.md` for report wording and endpoint examples.

## Frontend State

The frontend was migrated to Nuxt 3 with SSR. Current design direction:

- PrimeVue should be used as much as possible.
- Main layout uses a left sidebar/tree style navigation.
- UI colors, button styles, spacing, tables and charts have been repeatedly normalized.
- Room layout editor exists and supports room geometry, element placement, rotation, sizing, and clean view mode.
- Time range controls were redesigned with mutually exclusive presets/custom mode.
- Demo data page was redesigned from table overflow into responsive rows/cards.

Known UX areas still worth auditing:

- Dashboard empty-state creation flow.
- Auth redirect behavior on first load.
- Consistency of error feedback in create/edit dialogs.
- Full smoke test of demo data controls after backend changes.

## Current Cluster State

Expected running workloads in namespace `airsense`:

- `airsense-api`
- `airsense-web`
- `telemetry-ingestion`
- `automation`
- `notification`
- `device-telemetry-simulator`
- `ai-prediction-service`
- `emqx`
- `redis`
- `postgres-0`
- periodic/completed `ai-training-job-*`

Useful commands:

```bash
cd /opt/airsense
kubectl get deploy,sts,pods,svc,ingress -n airsense
kubectl logs -n airsense deployment/airsense-api --tail=120
kubectl logs -n airsense deployment/airsense-web --tail=120
kubectl exec -n airsense postgres-0 -- psql -U postgres -d airsensedb -Atc 'select id,email,name,uid from users order by id desc limit 10;'
```

## Deployment Commands

Full local Minikube deployment:

```bash
cd /opt/airsense
./scripts/deploy-minikube.sh
```

Rebuild API only:

```bash
cd /opt/airsense
eval "$(minikube -p airsense docker-env)"
docker build -f api/Airsense.API/Dockerfile --target final -t airsense-api:local api
kubectl rollout restart deployment/airsense-api -n airsense
kubectl rollout status deployment/airsense-api -n airsense --timeout=120s
```

Rebuild web only:

```bash
cd /opt/airsense
eval "$(minikube -p airsense docker-env)"
docker build -f web/Dockerfile -t airsense-web:local web
kubectl rollout restart deployment/airsense-web -n airsense
kubectl rollout status deployment/airsense-web -n airsense --timeout=120s
```

## Current Uncommitted Work

There are intentional uncommitted changes across:

- API auth/user/environment handling.
- Demo data API and simulator/AI integration.
- Database initialization and upgrade SQL.
- Helm chart values/templates.
- Nuxt auth middleware/store and demo data UI.
- Documentation copied into `docs/`.

Do not discard these changes. If committing later, inspect and split logically.

## Current Bug Thread: Environment Creation

Reported UI error:

```text
An error occurred while creating the environment
```

Root cause found earlier:

- `environment_members.member_id` references `users.id`.
- Firebase token/user state could contain a user id that did not exist in the reset/seeded PostgreSQL database.
- Initial failure was a PostgreSQL foreign key error on `environment_members_member_id_fkey`.
- Later UI test showed status `400`, likely because the current token did not have the custom `id` claim yet.

Fix work in progress:

- `AuthController` was changed to sync/create users and update Firebase custom claim id.
- `IUserRepository` was extended with `IsExistsByIdAsync` and `CreateWithIdAsync`.
- `UserRepository` implements explicit-id user creation and sequence reset.
- `EnvironmentController` now resolves the current user from token claims:
  - if claim `id` exists and DB row exists, use it;
  - if `uid/email` match an existing DB user, use that DB id;
  - if claim `id` exists but DB row does not, create the user with that id;
  - if claim `id` is absent, create a normal DB user from `uid/email`.

Latest verification on 2026-06-18 around 17:20 UTC:

- Docker builds passed for API, web, AI prediction service and device telemetry simulator.
- Helm lint and Helm template passed for `charts/airsense`.
- Public `https://airsense.yooud.org/api/healthz` returned `{"status":"ok","role":"api"}`.
- Kubernetes workloads in namespace `airsense` were ready, including `ai-prediction-service` and `device-telemetry-simulator`.
- AI health inside the cluster returned trained model `rf-20260618170002`.
- Controlled smoke test created a temporary Firebase user and verified:
  - `POST /api/env` succeeds with a Firebase token that has no custom `id` claim;
  - `POST /api/auth` syncs the user/custom claim path and returns 204 for an existing user;
  - `POST /api/env` succeeds again after auth sync;
  - temporary environments, PostgreSQL smoke user and Firebase smoke users were cleaned up.

Additional Playwright verification on 2026-06-18 around 17:46 UTC:

- A headless Chromium smoke test was run through the public UI at `https://airsense.yooud.org`.
- The final passing flow created a temporary Firebase email/password user, logged in through `/login`, opened `/dashboard`, created an environment through the `Create environment` dialog, and reached `/dashboard/env/7/rooms`.
- The run reported no API 4xx/5xx responses and no browser console errors.
- Temporary environment, Firebase user and PostgreSQL user rows were cleaned up. Follow-up DB check returned zero `pw.%@example.com` users and zero `Pw%` environments.
- Screenshots from the last run are available on the remote host under `/tmp/airsense-playwright/`.

Playwright also found two backend auth issues, both fixed in the current working tree and deployed to Minikube:

- `AuthController` no longer inserts `users.name = null` when Firebase email/password tokens omit the `name` claim; it falls back to email and validates required `uid`/`email` claims.
- `UserRepository.CreateAsync` now upserts on `uid`, making repeated or parallel `/auth` calls idempotent instead of failing on `users_uid_key`.

Remaining manual UI check, if needed:

1. Open `https://airsense.yooud.org/dashboard?env-create-retarget=1`.
2. Create a new environment from the empty dashboard.
3. If it fails, inspect browser console for status code and API logs:

```bash
kubectl logs -n airsense deployment/airsense-api --tail=200
kubectl exec -n airsense postgres-0 -- psql -U postgres -d airsensedb -Atc 'select id,email,name,uid from users order by id desc limit 10;'
```

## Working Rules For The Next Session

- Continue from `/opt/airsense`; do not restart the task from scratch.
- Do not mass-format DOCX files or project files.
- Do not commit after every action. Commit only when explicitly requested.
- Preserve user-made changes and unrelated dirty files.
- Prefer focused tests and smoke checks after each functional change.
- For frontend checks, use the in-app browser against `https://airsense.yooud.org` or `http://10.10.0.42:18080`.
- For diploma report text, write in Ukrainian and avoid saying the system classifies data as synthetic. Say the simulator imitates normal device operation for demonstration.
