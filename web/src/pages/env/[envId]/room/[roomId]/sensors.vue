<template>
  <RoomSensors />
</template>

<script setup lang="ts">
definePageMeta({ name: 'room-sensors', requiresAuth: true })
import RoomSensors from '@/components/room/RoomSensors.vue'
</script>
