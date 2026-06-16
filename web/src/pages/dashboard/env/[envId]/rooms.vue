<template>
  <RoomsList />
</template>

<script setup lang="ts">
definePageMeta({ name: 'environment-rooms', requiresAuth: true })
import RoomsList from '@/components/environment/RoomsList.vue'
</script>
