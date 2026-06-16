<template>
  <MembersList />
</template>

<script setup lang="ts">
definePageMeta({ name: 'environment-members', requiresAuth: true })
import MembersList from '@/components/environment/MembersList.vue'
</script>
