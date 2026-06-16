<template>
  <div class="flex flex-col flex-grow"
    :class="{ 'place-content-center': environment?.role === 'user' }"
  >
    <div
      v-if="environment?.role === 'user'"
      class="flex flex-col flex-grow justify-center items-center max-w-lg self-center"
    >
      <i class="pi pi-lock text-6xl text-gray-300 mb-4"></i>
      <h1 class="text-2xl font-semibold text-gray-800 mb-2 text-center">
        You are not allowed to access this room
      </h1>
      <p class="text-gray-600 text-center">
        Please contact your administrator if you believe this is an error.
      </p>
    </div>

    <div v-else>
      <div class="flex justify-between items-center mt-8">
        <h1 v-if="room" class="text-3xl font-bold text-gray-800">{{ room.name }}</h1>
        <div class="flex items-center gap-2">
          <Button label="Edit" icon="pi pi-pencil" rounded variant="text" @click="editRoomDialog = true" />
          <Button label="Delete" icon="pi pi-trash" rounded severity="danger" @click="deleteRoom" variant="text" />
      </div>
      </div>

      <div v-if="isLoading" class="space-y-2">
        <div class="h-10 bg-gray-300 animate-pulse rounded"></div>
      </div>

      <Tabs :value="activeTab" class="mt-8">
        <TabList qwety="qwety" :pt="{ tabList: 'bg-inherit'}">
          <Tab v-for="tab in items" :key="tab.label" :value="tab.value">
            <router-link v-if="tab.route" v-slot="{ href, navigate }" :to="tab.route" custom>
              <a :href="href" @click="navigate" class="flex items-center gap-2 text-inherit">
                <i :class="tab.icon" />
                <span>{{ tab.label }}</span>
              </a>
            </router-link>
          </Tab>
        </TabList>
      </Tabs>

      <div class="my-6 flex flex-grow">
        <router-view />
      </div>
    </div>

    <edit-room-dialog v-model="editRoomDialog" :envId="envId" :roomId="roomId" @refresh="refreshRoom" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRoom, removeRoom as deleteRoomApi } from "@/services/apiService";
import type { Environment } from "@/types/environment";
import type { Room } from "@/types/room";
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';
import Button from 'primevue/button';
import EditRoomDialog from "@/components/room/EditRoomDialog.vue";
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const roomId = Number(route.params.roomId);

const environmentStore = useEnvironmentStore();
const environment = ref<Environment | null>(null);
const room = ref<Room | null>(null);
const editRoomDialog = ref(false);
const isLoading = ref(true);
const confirm = useConfirm();
const toast = useToast();

const activeTab = ref<"parameters" | "sensors" | "devices" | "settings">(
    route.path.includes("sensors")
        ? "sensors"
        : route.path.includes("devices")
            ? "devices"
            : route.path.includes("settings")
                ? "settings"
                : "parameters"
);

const items = ref([
  { route: { name: 'room-parameters' }, label: 'Parameters', icon: 'pi pi-chart-line', value: 'parameters' },
  { route: { name: 'room-sensors' }, label: 'Sensors', icon: 'pi pi-bullseye', value: 'sensors' },
  { route: { name: 'room-devices' }, label: 'Devices', icon: 'pi pi-slack', value: 'devices' },
  { route: { name: 'room-settings' }, label: 'Settings', icon: 'pi pi-cog', value: 'settings' },
]);

const refreshRoom = async () => {
  isLoading.value = true;
  room.value = await getRoom(envId, roomId);
  isLoading.value = false;
}

const deleteRoom = async () => {
  confirm.require({
        message: 'Are you sure you want to delete this room?',
        header: 'Confirmation',
        icon: 'pi pi-exclamation-triangle',
        rejectProps: {
          label: 'Cancel',
          severity: 'secondary',
          outlined: true
        },
        acceptProps: {
          label: 'Delete',
          severity: 'danger'
        },
        accept: async () => {
          await deleteRoomApi(envId, roomId);
          router.push({ name: 'environment-rooms' });
          toast.add({ severity: 'success', summary: 'Success', detail: 'Room successfully deleted', life: 3000 });
        },
      });
}

onMounted(async () => {
  if (route.name === "room") {
    await router.replace({ name: "room-parameters" });
    activeTab.value = "parameters";
  }

  isLoading.value = true;
  environment.value = await environmentStore.fetchEnvironment(envId);
  if (environment.value?.role === "user") {
    isLoading.value = false;
    return;
  }
  room.value = await getRoom(envId, roomId);
  isLoading.value = false;
});
</script>
