<template>
  <RoomLayoutEditor />
</template>

<script setup lang="ts">
definePageMeta({ name: "room-layout", requiresAuth: true });

import RoomLayoutEditor from "@/components/room/RoomLayoutEditor.vue";
</script>
