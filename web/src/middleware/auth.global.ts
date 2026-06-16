import { auth } from '@/firebase'
import { useAuthStore } from '@/store/authStore'
import type { User } from 'firebase/auth'

const waitForFirebaseUser = () =>
  new Promise<User | null>((resolve) => {
    const unsubscribe = auth.onAuthStateChanged((user) => {
      unsubscribe()
      resolve(user)
    })
  })

export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) return

  const authStore = useAuthStore()
  const user = await waitForFirebaseUser()
  authStore.user = user

  if (to.meta.requiresAuth && !user) {
    return navigateTo('/login')
  }

  if (to.meta.guestOnly && user) {
    return navigateTo('/dashboard')
  }
})
