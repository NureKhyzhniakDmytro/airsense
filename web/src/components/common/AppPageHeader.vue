<template>
  <section class="app-page-header">
    <div class="app-page-header__copy">
      <div v-if="eyebrow || $slots.eyebrow" class="app-page-header__eyebrow">
        <slot name="eyebrow">{{ eyebrow }}</slot>
      </div>

      <div class="app-page-header__title-row">
        <h1>{{ title }}</h1>
        <slot name="badge" />
      </div>

      <p v-if="description">{{ description }}</p>
    </div>

    <div v-if="$slots.actions" class="app-page-header__actions">
      <slot name="actions" />
    </div>
  </section>
</template>

<script setup lang="ts">
defineProps<{
  title: string;
  description?: string;
  eyebrow?: string;
}>();
</script>

<style scoped>
.app-page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  border-bottom: 1px solid var(--app-border);
  flex: 0 0 auto;
  gap: var(--app-gap-md);
  max-width: 100%;
  min-width: 0;
  padding-bottom: var(--app-gap-md);
  width: 100%;
}

.app-page-header__copy {
  flex: 1 1 auto;
  min-width: 0;
}

.app-page-header__eyebrow {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 600;
  line-height: 1rem;
  margin-bottom: 3px;
  text-transform: uppercase;
}

.app-page-header__title-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.app-page-header h1 {
  color: var(--app-text-strong);
  font-size: 1.35rem;
  font-weight: 780;
  line-height: 1.18;
  margin: 0;
  overflow-wrap: anywhere;
}

.app-page-header p {
  color: var(--app-muted);
  font-size: 0.875rem;
  line-height: 1.25rem;
  margin: 4px 0 0;
  max-width: 680px;
}

.app-page-header__actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--app-gap-sm);
  flex: 0 0 auto;
}

@media (max-width: 720px) {
  .app-page-header {
    flex-direction: column;
    gap: 16px;
  }

  .app-page-header__actions {
    justify-content: flex-start;
    width: 100%;
  }

  .app-page-header h1 {
    font-size: 1.2rem;
  }
}
</style>
