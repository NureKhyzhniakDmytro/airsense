<template>
  <div class="empty-state">
    <div class="empty-state__icon">
      <i :class="icon" />
    </div>

    <h2>{{ title }}</h2>
    <p>{{ description }}</p>

    <div v-if="actionLabel || $slots.actions" class="empty-state__actions">
      <slot name="actions">
        <Button
          :label="actionLabel"
          :icon="actionIcon"
          :disabled="disabled"
          @click="$emit('action')"
        />
      </slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import Button from 'primevue/button';

withDefaults(
  defineProps<{
    title: string;
    description: string;
    icon?: string;
    actionLabel?: string;
    actionIcon?: string;
    disabled?: boolean;
  }>(),
  {
    icon: 'pi pi-info-circle',
    actionIcon: undefined,
    actionLabel: undefined,
  },
);

defineEmits<{
  (event: 'action'): void;
}>();
</script>

<style scoped>
.empty-state {
  align-items: flex-start;
  align-self: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  justify-content: center;
  max-width: 560px;
  min-height: 220px;
  padding: 28px;
  text-align: left;
  width: 100%;
}

.empty-state--fill {
  align-self: stretch;
  flex: 1;
  max-width: none;
  min-height: 0;
}

.empty-state--centered {
  align-items: center;
  text-align: center;
}

.empty-state__icon {
  align-items: center;
  background: color-mix(in srgb, var(--app-primary) 10%, var(--app-surface-soft));
  border: 1px solid color-mix(in srgb, var(--app-primary) 24%, var(--app-border));
  border-radius: 5px;
  color: var(--app-primary);
  display: inline-flex;
  height: 42px;
  justify-content: center;
  margin-bottom: 16px;
  width: 42px;
}

.empty-state__icon i {
  font-size: 1.4rem;
}

.empty-state h2 {
  color: var(--app-text-strong);
  font-size: 1.05rem;
  font-weight: 780;
  line-height: 1.45rem;
  margin: 0;
}

.empty-state p {
  color: var(--app-muted);
  font-size: 0.875rem;
  line-height: 1.35rem;
  margin: 8px 0 0;
  max-width: 380px;
}

.empty-state--centered p {
  text-align: center;
}

.empty-state__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-start;
  margin-top: 18px;
}

.empty-state--centered .empty-state__actions {
  justify-content: center;
}
</style>
