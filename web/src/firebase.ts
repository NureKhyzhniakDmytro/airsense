import { initializeApp } from "firebase/app";
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut } from "firebase/auth";
import type { User } from "firebase/auth";
import { getMessaging, getToken, onMessage } from "firebase/messaging";

const runtimeConfig = useRuntimeConfig();

const firebaseConfig = {
  apiKey: runtimeConfig.public.firebaseApiKey || import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: runtimeConfig.public.firebaseAuthDomain || import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: runtimeConfig.public.firebaseProjectId || import.meta.env.VITE_FIREBASE_PROJECT_ID,
  storageBucket: runtimeConfig.public.firebaseStorageBucket || import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: runtimeConfig.public.firebaseMessagingSenderId || import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
  appId: runtimeConfig.public.firebaseAppId || import.meta.env.VITE_FIREBASE_APP_ID,
  measurementId: runtimeConfig.public.firebaseMeasurementId || import.meta.env.VITE_FIREBASE_MEASUREMENT_ID,
};

const app = initializeApp(firebaseConfig);

const messaging = getMessaging(app);

export const auth = getAuth(app);
export type AuthUser = User | null;
export const googleProvider = new GoogleAuthProvider();

export const getFcmToken = async (): Promise<string | null> => {
  try {
    const token = await getToken(messaging, {
      vapidKey: runtimeConfig.public.firebaseVapidKey || import.meta.env.VITE_FIREBASE_VAPID_KEY,
    });
    if (token) {
      console.log('FCM Token:', token);
      return token;
    } else {
      console.warn('?? ??????? ???????? ?????');
      return null;
    }
  } catch (error) {
    console.error('?????? ??? ????????? ??????:', error);
    return null;
  }
};

export const onMessageListener = (callback: (payload: any) => void) => {
  onMessage(messaging, (payload) => {
    console.log("???????? ?????????:", payload);
    callback(payload);
  });
};

export const signInWithGoogle = async (): Promise<User | null> => {
  try {
    const result = await signInWithPopup(auth, googleProvider);
    return result.user;
  } catch (error) {
    console.error("?????? ????? ????? Google:", error);
    throw error;
  }
};

export const logout = async () => {
  await signOut(auth);
};
