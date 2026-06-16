import { useAuthStore } from '@/store/authStore'

export default defineNuxtRouteMiddleware(async (to) => {
  const authStore = useAuthStore()

  if (import.meta.server) {
    authStore.hydrateFromCookie()
  } else {
    await authStore.startAuthListener()
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return navigateTo('/login')
  }

  if (to.meta.guestOnly && authStore.isAuthenticated) {
    return navigateTo('/dashboard')
  }
})
