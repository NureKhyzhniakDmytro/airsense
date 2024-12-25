<template>
  <div class="p-6 max-w-md mx-auto bg-white rounded-lg shadow-lg">
    <h1 class="text-3xl font-bold text-center text-gray-800">Firebase Notifications</h1>
    <div class="mt-6 text-center">
      <button
          @click="enableNotifications"
          class="px-6 py-3 bg-blue-500 text-white rounded-md hover:bg-blue-600 transition"
      >
        Enable notifications
      </button>
    </div>

    <div class="mt-6 text-center">
      <button
          @click="signInWithGoogle"
          class="px-6 py-3 bg-red-500 text-white rounded-md hover:bg-red-600 transition"
      >
        Sign in with Google
      </button>
    </div>

    <div v-if="user" class="mt-8 text-center">
      <h2 class="text-xl font-semibold text-gray-700">Hello, {{ user.displayName }}</h2>
      <img :src="user.photoURL" alt="User Photo" class="rounded-full w-24 h-24 mt-4 mx-auto" />
      <p class="mt-4 text-gray-500">{{ user.email }}</p>

      <div class="mt-6 space-y-4">
        <div class="bg-gray-100 p-4 rounded-md shadow-sm">
          <h3 class="font-semibold text-gray-700">FCM Token:</h3>
          <div class="flex items-center mt-2">
            <span class="flex-1 text-sm text-gray-600 overflow-hidden line-clamp-1">{{ fcmToken }}</span>
            <button
                @click="copyToClipboard(fcmToken)"
                class="ml-2 text-blue-500 hover:text-blue-600"
            >
              Copy
            </button>
          </div>
        </div>

        <div class="bg-gray-100 p-4 rounded-md shadow-sm">
          <h3 class="font-semibold text-gray-700">JWT (ID) Token:</h3>
          <div class="flex items-center mt-2">
            <span class="flex-1 text-sm text-gray-600 overflow-hidden line-clamp-1">{{ idToken }}</span>
            <button
                @click="copyToClipboard(idToken)"
                class="ml-2 text-blue-500 hover:text-blue-600"
            >
              Copy
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="notifications.length > 0" class="mt-8 space-y-4">
      <div
          v-for="(notification, index) in notifications"
          :key="index"
          class="p-4 bg-gray-50 rounded-md shadow-sm"
      >
        <h3 class="font-semibold text-gray-700">{{ notification.title }}</h3>
        <p class="text-gray-600">{{ notification.body }}</p>
      </div>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, ref } from 'vue';
import { getFcmToken, onMessageListener, signInWithGoogle } from './firebase';
import { User } from 'firebase/auth';

interface Notification {
  title: string;
  body: string;
}

export default defineComponent({
  setup() {
    const notifications = ref<Notification[]>([]);
    const user = ref<User | null>(null);
    const fcmToken = ref<string | null>(null);
    const idToken = ref<string | null>(null);

    const enableNotifications = async (): Promise<void> => {
      const token = await getFcmToken();
      if (token) {
        fcmToken.value = token;
        console.log('Токен устройства:', token);
      }
    };

    const signIn = async (): Promise<void> => {
      const loggedInUser = await signInWithGoogle();
      user.value = loggedInUser;
      if (user.value) {
        // Get the Firebase ID token (JWT token)
        idToken.value = await user.value.getIdToken();
      }
    };

    const copyToClipboard = (text: string | null): void => {
      if (text) {
        navigator.clipboard.writeText(text).then(() => {
          alert('Токен скопирован!');
        });
      }
    };

    onMessageListener((payload: any): void => {
      const notification: Notification = {
        title: payload.notification?.title || 'Новое уведомление',
        body: payload.notification?.body || '',
      };
      notifications.value.push(notification);
    });

    return {
      enableNotifications,
      signInWithGoogle: signIn,
      notifications,
      user,
      fcmToken,
      idToken,
      copyToClipboard,
    };
  },
});
</script>
