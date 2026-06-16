<template>
  <RoomDevices />
</template>

<script setup lang="ts">
definePageMeta({ name: 'room-devices', requiresAuth: true })
import RoomDevices from '@/components/room/RoomDevices.vue'
</script>
