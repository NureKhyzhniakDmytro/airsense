<template>
  <section class="metric-card">
    <span class="metric-card__icon">
      <i :class="icon" />
    </span>
    <div class="metric-card__copy">
      <div class="metric-card__label">{{ label }}</div>
      <div class="metric-card__value">{{ value }}</div>
      <div v-if="hint" class="metric-card__hint">{{ hint }}</div>
    </div>
  </section>
</template>

<script setup lang="ts">
defineProps<{
  label: string;
  value: string | number;
  icon: string;
  hint?: string;
}>();
</script>

<style scoped>
.metric-card {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  display: flex;
  gap: 12px;
  min-height: 78px;
  padding: 12px;
}

.metric-card__icon {
  align-items: center;
  background: color-mix(in srgb, var(--app-primary) 10%, var(--app-surface-soft));
  border: 1px solid color-mix(in srgb, var(--app-primary) 22%, var(--app-border));
  border-radius: 5px;
  color: var(--app-primary);
  display: inline-flex;
  flex: 0 0 auto;
  height: 36px;
  justify-content: center;
  width: 36px;
}

.metric-card__copy {
  min-width: 0;
}

.metric-card__label {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 600;
  line-height: 1.15rem;
  text-transform: uppercase;
}

.metric-card__value {
  color: var(--app-text-strong);
  font-size: 1.45rem;
  font-weight: 800;
  line-height: 1.75rem;
}

.metric-card__hint {
  color: var(--app-muted);
  font-size: 0.78rem;
  line-height: 1.15rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
