<template>
  <span class="place-icon" :class="[`place-icon--${size}`, `place-icon--${tone}`]" aria-hidden="true">
    <span class="material-symbols-outlined">{{ option.symbol }}</span>
  </span>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { getPlaceIconOption } from "@/config/placeOptions";

const props = withDefaults(defineProps<{
  name?: string | null;
  size?: "sm" | "md" | "lg";
  tone?: "light" | "dark";
}>(), {
  name: "building",
  size: "md",
  tone: "light",
});

const option = computed(() => getPlaceIconOption(props.name));
</script>

<style scoped>
.place-icon {
  align-items: center;
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: 5px;
  color: var(--app-primary-strong);
  display: inline-flex;
  flex: 0 0 auto;
  justify-content: center;
}

.place-icon--sm {
  height: 30px;
  width: 30px;
}

.place-icon--md {
  height: 36px;
  width: 36px;
}

.place-icon--lg {
  height: 44px;
  width: 44px;
}

.place-icon--dark {
  background: var(--app-sidebar-subtle);
  border-color: var(--app-sidebar-subtle-strong);
  color: var(--app-sidebar-active);
}

.place-icon :deep(.material-symbols-outlined) {
  font-size: 1.3rem;
}

.place-icon--sm :deep(.material-symbols-outlined) {
  font-size: 1.1rem;
}

.place-icon--lg :deep(.material-symbols-outlined) {
  font-size: 1.55rem;
}
</style>
