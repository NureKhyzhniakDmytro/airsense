import { createRouter, createWebHistory, RouteRecordRaw } from "vue-router";
import { auth } from "@/firebase";
import { useAuthStore } from "@/store/authStore";
import Dashboard from "@/pages/Dashboard.vue";
import EnvironmentDetails from "@/pages/EnvironmentDetails.vue";
import Login from "@/pages/Login.vue";
import Register from "@/pages/Register.vue";
import RoomDetails from "@/pages/RoomDetails.vue";
import RoomsList from "@/components/RoomsList.vue";
import MembersList from "@/components/MembersList.vue";
import {User} from "firebase/auth";

const routes: RouteRecordRaw[] = [
  { path: "/", component: Dashboard, meta: { requiresAuth: true } },
  { path: "/login", component: Login, meta: { guestOnly: true } },
  { path: "/register", component: Register, meta: { guestOnly: true } },
  {
    path: "/env/:envId",
    component: EnvironmentDetails,
    meta: { requiresAuth: true },
    props: true,
    children: [
      {
        path: 'rooms',
        name: 'rooms',
        component: RoomsList,
      },
      {
        path: 'members',
        name: 'members',
        component: MembersList,
      },
    ]
  },
  {
    path: "/env/:envId/room/:roomId",
    component: RoomDetails,
    props: true,
    meta: { requiresAuth: true },
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
    component: () => import("@/pages/SensorDetails.vue"),
    props: true,
    meta: { requiresAuth: true },
  },
  {
    path: "/env/:envId/room/:roomId/devices/:deviceId",
    component: () => import("@/pages/DeviceDetails.vue"),
    props: true,
    meta: { requiresAuth: true },
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
