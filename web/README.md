# AirSense Web

The web client is implemented with Nuxt 3 in SPA mode. It keeps the existing Vue 3 dashboard UI while using Nuxt file-based routing, layouts, plugins, and runtime configuration.

## Stack

- Nuxt 3
- Vue 3 + TypeScript
- Pinia
- PrimeVue
- Tailwind CSS / DaisyUI
- Firebase Auth and Firebase Cloud Messaging
- Axios API client
- ApexCharts for telemetry charts

## Local Development

```bash
npm install
npm run dev
```

The API base URL and Firebase values are read from Nuxt public runtime variables. The legacy `VITE_*` names are still accepted for local compatibility.

```bash
NUXT_PUBLIC_API_BASE_URL=http://localhost:8080
NUXT_PUBLIC_FIREBASE_API_KEY=
NUXT_PUBLIC_FIREBASE_AUTH_DOMAIN=
NUXT_PUBLIC_FIREBASE_PROJECT_ID=
NUXT_PUBLIC_FIREBASE_STORAGE_BUCKET=
NUXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID=
NUXT_PUBLIC_FIREBASE_APP_ID=
NUXT_PUBLIC_FIREBASE_MEASUREMENT_ID=
NUXT_PUBLIC_FIREBASE_VAPID_KEY=
```

## Build

```bash
npm run build
npm run preview
```

The application is currently built as a Nuxt SPA (`ssr: false`) because authentication and Firebase messaging are browser-oriented.
