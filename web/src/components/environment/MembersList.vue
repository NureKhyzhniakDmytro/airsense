<template>
  <div class="section-page">
    <AppSectionHeader
      title="Members"
      description="Users with access to this environment."
    >
      <template #actions>
      <Button
          @click="inviteMemberDialog = true"
          label="Invite"
          icon="pi pi-plus"
          v-if="!isEnvironmentReadOnly"
      />
      </template>
    </AppSectionHeader>

    <section class="members-panel">
        <ContextMenu ref="contextMenu" :model="items" />
        <DataTable
            class="members-table"
            :value="members"
            :first="pagination.skip"
            paginator
            lazy
            scrollable
            scrollHeight="flex"
            dataKey="id"
            :loading="isLoading"
            @page="changePage"
            :rows="pagination.count"
            :total-records="pagination.total"
        >
          <Column field="name" header="Name" style="min-width: 12rem" />
          <Column field="email" header="Email" style="min-width: 16rem" />
          <Column field="role" header="Role" style="width: 8rem">
            <template #body="slotProps">
              <RoleTag v-if="slotProps.data" :role="slotProps.data.role" />
            </template>
          </Column>
          <Column style="width: 3.5rem">
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

          <template #empty>
            <div class="members-empty">
              <i class="pi pi-users" />
              <span>No members yet</span>
            </div>
          </template>
        </DataTable>
    </section>

    <invite-member-dialog v-model="inviteMemberDialog" :envId="envId" @refresh="refreshMembers" />
    <edit-member-dialog v-if="selectedMember" v-model="editMemberDialog" :env-id="envId" :member="selectedMember" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { getMembers, removeUser } from "@/services/apiService";
import type { Environment } from "@/types/environment";
import { useEnvironmentStore } from "@/store/environmentStore";
import { useAuthStore } from "@/store/authStore";
import AppSectionHeader from "@/components/common/AppSectionHeader.vue";
import RoleTag from "@/components/common/RoleTag.vue";
import DataTable from 'primevue/datatable';
import type { DataTablePageEvent } from 'primevue/datatable';
import Column from 'primevue/column';
import Button from "primevue/button";
import InviteMemberDialog from "@/components/environment/InviteMemberDialog.vue";
import { useConfirm } from "primevue/useconfirm";
import type { MenuItemCommandEvent } from "primevue/menuitem";
import ContextMenu from 'primevue/contextmenu';
import type { ContextMenuMethods } from 'primevue/contextmenu';
import { useToast } from 'primevue/usetoast';
import EditMemberDialog from "@/components/environment/EditMemberDialog.vue";
import type { User } from "@/types/user";
import { isReadOnlyRole } from "@/utils/roomAccess";

const route = useRoute();
const authStore = useAuthStore();
const members = ref<User[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 12 });
const environment = ref<Environment | null>(null);
const environmentStore = useEnvironmentStore();
const envId = Number(route.params.envId);
const inviteMemberDialog = ref(false);
const editMemberDialog = ref(false);
const isLoading = ref(false);
const contextMenu = ref<ContextMenuMethods | null>(null);
const selectedRow = ref<User | null>(null);
const confirm = useConfirm();
const toast = useToast();
const selectedMember = ref<User | null>(null);

const currentUserEmail = computed(() => authStore.currentUserEmail);
const isEnvironmentReadOnly = computed(() => !environment.value || isReadOnlyRole(environment.value.role));

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

const canModify = (user: User) => !isEnvironmentReadOnly.value && user.role !== "owner" && user.email !== currentUserEmail.value;

const deleteUser = async (user: User) => {
  try {
    await removeUser(envId, user.id);
    members.value = members.value.filter(u => u.id !== user.id);
  } catch (error) {
    console.error("User deletion error:", (error as Error).message);
  }
};

const changePage = async (event: DataTablePageEvent | { first?: number }) => {
  isLoading.value = true;
  if (!environment.value) {
    environment.value = await environmentStore.fetchEnvironment(envId);
  }

  pagination.value.skip = event.first ?? 0;
  const { members: memberList, pagination: pag } = await getMembers(environment.value.id, pagination.value.skip, pagination.value.count);
  members.value = memberList;
  pagination.value = pag;
  isLoading.value = false;
};

const refreshMembers = async () => {
  await changePage({ first: pagination.value.skip });
};

onMounted(refreshMembers);
</script>

<style scoped>
.section-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.members-panel {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.members-table {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
}

.members-table :deep(.p-datatable-table-container) {
  flex: 1;
  min-height: 0;
}

.members-table :deep(.p-paginator) {
  border-top: 1px solid var(--app-border);
  flex: 0 0 auto;
}

.members-table :deep(.p-datatable-tbody > tr > td) {
  height: 48px;
  padding-bottom: 8px;
  padding-top: 8px;
}

.members-table :deep(.p-datatable-emptymessage > td) {
  height: 360px;
}

.members-empty {
  align-items: center;
  color: var(--app-muted);
  display: flex;
  flex-direction: column;
  gap: 8px;
  justify-content: center;
  min-height: 180px;
}

.members-empty i {
  color: var(--app-primary);
  font-size: 1.5rem;
}
</style>
