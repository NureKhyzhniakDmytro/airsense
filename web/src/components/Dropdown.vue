<template>
  <div class="relative" :class="wrapperClass">
    <button
        @click="toggle"
        :class="['w-full flex justify-between items-center', buttonClass]"
    >
      <span>{{ selectedLabel }}</span>
      <svg class="w-4 h-4 ml-2" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" d="M19 9l-7 7-7-7" />
      </svg>
    </button>

    <transition name="fade">
      <ul
          v-if="open"
          :class="['absolute mt-1 z-10 max-h-60 overflow-auto', listClass]"
      >
        <li
            v-for="(option, index) in options"
            :key="index"
            @click="select(option)"
            :class="[
            itemClass,
            { [activeItemClass]: option.value === modelValue }
          ]"
        >
          {{ option.label }}
        </li>
      </ul>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, defineProps, defineEmits, onMounted, onUnmounted } from 'vue'

interface Option {
  label: string
  value: string | number
}

const props = defineProps<{
  modelValue: string | number
  options: Option[]

  buttonClass?: string
  listClass?: string
  itemClass?: string
  activeItemClass?: string
  wrapperClass?: string
}>()

const emit = defineEmits(['update:modelValue'])

const open = ref(false)

const toggle = () => {
  open.value = !open.value
}

const close = () => {
  open.value = false
}

const select = (option: Option) => {
  emit('update:modelValue', option.value)
  close()
}

const selectedLabel = computed(() => {
  const found = props.options.find(o => o.value === props.modelValue)
  return found?.label || 'Select...'
})

const onClickOutside = (e: MouseEvent) => {
  const path = e.composedPath()
  if (!(path as HTMLElement[]).some(el => (el as HTMLElement).classList?.contains('relative'))) {
    close()
  }
}

onMounted(() => {
  document.addEventListener('click', onClickOutside)
})
onUnmounted(() => {
  document.removeEventListener('click', onClickOutside)
})
</script>

<style scoped>
.fade-enter-active, .fade-leave-active {
  transition: opacity 0.15s ease;
}
.fade-enter-from, .fade-leave-to {
  opacity: 0;
}
</style>
