<template>
  <RoomSettings />
</template>

<script setup lang="ts">
definePageMeta({ name: 'room-settings', requiresAuth: true })
import RoomSettings from '@/components/room/RoomSettings.vue'
</script>
