<template>
  <div class="items-center flex-grow">

    <div class="flex justify-between items-center">
      <div>
        <h2 class="text-xl font-semibold text-gray-800">Members</h2>
        <h2 class="text-sm text-gray-500">List of all users in your environment.</h2>
      </div>

      <Button
          @click="inviteMemberDialog = true"
          label="Invite"
          icon="pi pi-plus"
          :disabled="environment?.role === 'user'"
      />
      <invite-member-dialog v-model="inviteMemberDialog" :envId="envId" @refresh="refreshMembers" />
    </div>

    <Card class="mt-8" :pt="{ body: 'p-0' }" >
      <template #content>
        <ContextMenu ref="contextMenu" :model="items" />
        <DataTable
            :value="members"
            paginator
            @page="changePage"
            :rows="pagination.count"
            :total-records="pagination.total"
            class="mt-3 rounded-t-xl"
            :pt="{
              pcPaginator: {
                root: 'rounded-b-xl rounded-none'
              }
            }"
        >
          <Column field="name" header="Name"/>
          <Column field="email" header="Email"/>
          <Column field="role" header="Role">
            <template #body="slotProps">
              <span
                  v-if="slotProps.data"
                  class="px-3 py-1 rounded-full text-xs font-medium"
                  :class="getRoleBadge(slotProps.data.role)"
              >
                {{ slotProps.data.role }}
              </span>
            </template>
          </Column>
          <Column>
            <template #body="slotProps">
              <div v-if="slotProps.data" class="flex justify-end">
                <Button
                    :disabled="!canModify(slotProps.data)"
                    icon="pi pi-ellipsis-v"
                    @click="onMenuButtonClick($event, slotProps.data)"
                    severity="secondary"
                    variant="text"
                    rounded
                    aria-haspopup="true"
                    aria-controls="overlay_menu"
                />
              </div>
            </template>
          </Column>
        </DataTable>
      </template>
    </Card>

    <edit-member-dialog v-if="selectedMember" v-model="editMemberDialog" :env-id="envId" :member="selectedMember" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { useRoute } from "vue-router";
import { getMembers, removeUser } from "@/services/apiService";
import type { Environment } from "@/types/environment";
import { getRoleBadge } from "@/utils/environment";
import { useEnvironmentStore } from "@/store/environmentStore";
import { useAuthStore } from "@/store/authStore";
import Card from 'primevue/card';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Button from "primevue/button";
import InviteMemberDialog from "@/components/environment/InviteMemberDialog.vue";
import { useConfirm } from "primevue/useconfirm";
import type { MenuItemCommandEvent } from "primevue/menuitem";
import ContextMenu from 'primevue/contextmenu';
import type { ContextMenuMethods } from 'primevue/contextmenu';
import type { DataViewPageEvent } from "primevue/dataview";
import { useToast } from 'primevue/usetoast';
import EditMemberDialog from "@/components/environment/EditMemberDialog.vue";
import type { User } from "@/types/user";

const route = useRoute();
const authStore = useAuthStore();
const members = ref<User[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 5 });
const environment = ref<Environment | null>(null);
const environmentStore = useEnvironmentStore();
const envId = Number(route.params.envId);
const inviteMemberDialog = ref(false);
const editMemberDialog = ref(false);
const contextMenu = ref<ContextMenuMethods | null>(null);
const selectedRow = ref<User | null>(null);
const confirm = useConfirm();
const toast = useToast();
const selectedMember = ref<User | null>(null);

const currentUserEmail = computed(() => authStore.currentUserEmail);

const items = ref([
  {
    label: 'Edit',
    icon: 'pi pi-pen-to-square',
    command: (event: MenuItemCommandEvent) => {
      selectedMember.value = selectedRow.value;
      contextMenu.value?.hide();
      editMemberDialog.value = true;
    }
  },
  {
    label: 'Delete',
    icon: 'pi pi-trash',
    command: (event: MenuItemCommandEvent) => {
      confirm.require({
        message: 'Do you want to delete this record?',
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
          if (selectedRow.value) {
            await deleteUser(selectedRow.value);
            toast.add({ severity: 'success', summary: 'Success', detail: 'User successfully deleted', life: 3000 });
          }
        },
      });
    }
  }
]);

const { data: initialMembersData } = await useAsyncData(
  `environment-${envId}-members-page-0`,
  async () => {
    const env = await environmentStore.fetchEnvironment(envId);
    const result = await getMembers(envId, 0, pagination.value.count);
    return { environment: env, ...result };
  },
);

if (initialMembersData.value) {
  environment.value = initialMembersData.value.environment;
  members.value = initialMembersData.value.members;
  pagination.value = initialMembersData.value.pagination;
}

function onMenuButtonClick(event: Event, rowData: User) {
  selectedRow.value = rowData;
  contextMenu.value?.show(event);
}

const canModify = (user: User) => user.role !== "owner" && user.email !== currentUserEmail.value;

const deleteUser = async (user: User) => {
  try {
    await removeUser(envId, user.id);
    members.value = members.value.filter(u => u.id !== user.id);
  } catch (error) {
    console.error("User deletion error:", (error as Error).message);
  }
};

const changePage = async (event: DataViewPageEvent) => {
  if (!environment.value) {
    environment.value = await environmentStore.fetchEnvironment(envId);
  }

  pagination.value.skip = event.first ?? 0;
  const { members: memberList, pagination: pag } = await getMembers(environment.value.id, pagination.value.skip, pagination.value.count);
  members.value = memberList;
  pagination.value = pag;
};

const refreshMembers = async () => {
  await changePage({ first: pagination.value.skip } as DataViewPageEvent);
};
</script>
