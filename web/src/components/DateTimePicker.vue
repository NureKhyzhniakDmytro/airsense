<template>
  <div :class="['relative', wrapperClass]">
    <button
        @click="toggle"
        :class="['w-full flex justify-between items-center', buttonClass]"
    >
      <span>{{ displayText }}</span>
      <svg class="w-4 h-4 ml-2" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" d="M19 9l-7 7-7-7" />
      </svg>
    </button>

    <transition name="fade">
      <div
          v-if="open"
          :class="['absolute mt-1 z-10 w-full shadow-lg rounded-md', listClass]"
      >
        <div class="p-3 space-y-2">
          <template v-if="!range">
            <div class="space-y-2">
              <input
                  v-if="showDate"
                  type="date"
                  v-model="singleDate"
                  :class="['w-full', inputClass]"
              />
              <input
                  v-if="showTime"
                  type="time"
                  v-model="singleTime"
                  :class="['w-full', inputClass]"
              />
            </div>
          </template>

          <template v-else>
            <div class="space-y-2">
              <div>
                <label class="text-sm font-semibold block">From:</label>
                <input
                    v-if="showDate"
                    type="date"
                    v-model="rangeStartDate"
                    :class="['w-full', inputClass]"
                />
                <input
                    v-if="showTime"
                    type="time"
                    v-model="rangeStartTime"
                    :class="['w-full', inputClass]"
                />
              </div>

              <div>
                <label class="text-sm font-semibold block">To:</label>
                <input
                    v-if="showDate"
                    type="date"
                    v-model="rangeEndDate"
                    :class="['w-full', inputClass]"
                />
                <input
                    v-if="showTime"
                    type="time"
                    v-model="rangeEndTime"
                    :class="['w-full', inputClass]"
                />
              </div>
            </div>
          </template>

          <div class="flex justify-between gap-2 pt-2">
            <button @click="confirm" :class="['w-full', confirmButtonClass]">OK</button>
            <button @click="close" :class="['w-full', cancelButtonClass]">Cancel</button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, defineProps, defineEmits, watch } from 'vue'

type ValueType = string | [string, string] | null

const props = defineProps<{
  modelValue: ValueType
  showDate?: boolean
  showTime?: boolean
  range?: boolean
  wrapperClass?: string
  buttonClass?: string
  listClass?: string
  inputClass?: string
  confirmButtonClass?: string
  cancelButtonClass?: string
}>()

const emit = defineEmits(['update:modelValue'])

const open = ref(false)
const toggle = () => open.value = !open.value
const close = () => open.value = false

const singleDate = ref('')
const singleTime = ref('')

const rangeStartDate = ref('')
const rangeStartTime = ref('')
const rangeEndDate = ref('')
const rangeEndTime = ref('')

watch(() => props.modelValue, () => {
  if (!props.range) {
    const date = props.modelValue ? new Date(props.modelValue as string) : new Date()
    singleDate.value = date.toISOString().split('T')[0]
    singleTime.value = date.toTimeString().slice(0, 5)
  } else if (Array.isArray(props.modelValue)) {
    const [startStr, endStr] = props.modelValue
    const start = new Date(startStr)
    const end = new Date(endStr)

    rangeStartDate.value = start.toISOString().split('T')[0]
    rangeStartTime.value = start.toTimeString().slice(0, 5)
    rangeEndDate.value = end.toISOString().split('T')[0]
    rangeEndTime.value = end.toTimeString().slice(0, 5)
  }
}, { immediate: true })

// Final output
const confirm = () => {
  if (!props.range) {
    let value = ''
    if (props.showDate) value += singleDate.value
    if (props.showTime) value += (value ? 'T' : '') + singleTime.value
    emit('update:modelValue', value || null)
  } else {
    let from = ''
    let to = ''
    if (props.showDate) {
      from += rangeStartDate.value
      to += rangeEndDate.value
    }
    if (props.showTime) {
      from += (from ? 'T' : '') + rangeStartTime.value
      to += (to ? 'T' : '') + rangeEndTime.value
    }
    emit('update:modelValue', [from, to])
  }

  close()
}

const displayText = computed(() => {
  if (!props.modelValue) return 'Select...'
  if (!props.range && typeof props.modelValue === 'string') {
    const date = new Date(props.modelValue)
    return formatDateTime(date)
  }
  if (props.range && Array.isArray(props.modelValue)) {
    const [startStr, endStr] = props.modelValue
    const start = new Date(startStr)
    const end = new Date(endStr)
    return `${formatDateTime(start)} → ${formatDateTime(end)}`
  }
  return 'Invalid'
})

const formatDateTime = (date: Date): string => {
  const parts = []
  if (props.showDate) parts.push(date.toLocaleDateString())
  if (props.showTime) parts.push(date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }))
  return parts.join(' ')
}
</script>

<style scoped>
.fade-enter-active, .fade-leave-active {
  transition: opacity 0.15s ease;
}
.fade-enter-from, .fade-leave-to {
  opacity: 0;
}
</style>
