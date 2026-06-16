<template>
  <section class="app-section-header">
    <div>
      <h2>{{ title }}</h2>
      <p v-if="description">{{ description }}</p>
    </div>

    <div v-if="$slots.actions" class="app-section-header__actions">
      <slot name="actions" />
    </div>
  </section>
</template>

<script setup lang="ts">
defineProps<{
  title: string;
  description?: string;
}>();
</script>

<style scoped>
.app-section-header {
  align-items: center;
  border-bottom: 1px solid var(--app-border);
  display: flex;
  flex: 0 0 auto;
  gap: var(--app-gap-md);
  justify-content: space-between;
  padding-bottom: var(--app-gap-sm);
  width: 100%;
}

.app-section-header h2 {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  font-weight: 780;
  line-height: 1.25rem;
  margin: 0;
}

.app-section-header p {
  color: var(--app-muted);
  font-size: 0.8125rem;
  line-height: 1.15rem;
  margin: 2px 0 0;
}

.app-section-header__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--app-gap-sm);
  justify-content: flex-end;
}

@media (max-width: 680px) {
  .app-section-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .app-section-header__actions {
    justify-content: flex-start;
  }
}
</style>
