import { createRouter, createWebHistory, RouteRecordRaw } from "vue-router";
import { auth } from "@/firebase";
import { useAuthStore } from "@/store/authStore";
import { User } from "firebase/auth";

const dashboardRoutes: RouteRecordRaw[] = [
  {
    path: "",
    name: "dashboard",
    component: () => import("@/pages/Dashboard.vue"),
  },
  {
    path: "env/:envId",
    name: "environment",
    component: () => import("@/pages/EnvironmentDetails.vue"),
    props: true,
    children: [
      {
        path: 'rooms',
        name: 'environment-rooms',
        component: () => import("@/components/environment/RoomsList.vue"),
      },
      {
        path: 'members',
        name: 'environment-members',
        component: () => import("@/components/environment/MembersList.vue"),
      },
    ]
  },
  {
    path: "/env/:envId/room/:roomId",
    name: "room",
    component: () => import("@/pages/RoomDetails.vue"),
    props: true,
    children: [
      {
        path: "parameters",
        name: "room-parameters",
        component: () => import("@/components/room/RoomCharts.vue"),
      },
      {
        path: "sensors",
        name: "room-sensors",
        component: () => import("@/components/room/RoomSensors.vue"),
      },
      {
        path: "devices",
        name: "room-devices",
        component: () => import("@/components/room/RoomDevices.vue"),
      },
      {
        path: "settings",
        name: "room-settings",
        component: () => import("@/components/room/RoomSettings.vue"),
      },
    ],
  },
  {
    path: "/env/:envId/room/:roomId/sensors/:sensorId",
    name: "sensor",
    component: () => import("@/pages/SensorDetails.vue"),
    props: true,
  },
  {
    path: "/env/:envId/room/:roomId/devices/:deviceId",
    name: "device",
    component: () => import("@/pages/DeviceDetails.vue"),
    props: true,
  }
]

const routes: RouteRecordRaw[] = [
  {
    path: "/",
    redirect: "/dashboard"
  },
  {
    path: "/login",
    component: () => import("@/pages/Login.vue"),
    meta: { guestOnly: true }
  },
  {
    path: "/register",
    component: () => import("@/pages/Register.vue"),
    meta: { guestOnly: true }
  },
  {
    path: "/dashboard",
    component: () => import("@/layout/DashboardLayout.vue"),
    meta: { requiresAuth: true },
    children: dashboardRoutes
  }
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async (to, _, next) => {
  const authStore = useAuthStore();

  const user = await new Promise((resolve) => auth.onAuthStateChanged(resolve));
  authStore.user = user as User;

  if (to.meta.requiresAuth && !user) {
    next("/login");
  } else if (to.meta.guestOnly && user) {
    next("/");
  } else {
    next();
  }
});

export default router;
