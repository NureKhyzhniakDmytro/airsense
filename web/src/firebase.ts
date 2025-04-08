import { initializeApp } from "firebase/app";
import { getAuth, GoogleAuthProvider, signInWithPopup, User, signOut } from "firebase/auth";
import { getMessaging, getToken, onMessage } from "firebase/messaging";

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID,
  measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID,
};

const app = initializeApp(firebaseConfig);

const messaging = getMessaging(app);

export const auth = getAuth(app);
export type AuthUser = User | null;
export const googleProvider = new GoogleAuthProvider();

export const getFcmToken = async (): Promise<string | null> => {
  try {
    const token = await getToken(messaging, {
      vapidKey: import.meta.env.VITE_FIREBASE_VAPID_KEY,
    });
    if (token) {
      console.log('FCM Token:', token);
      return token;
    } else {
      console.warn('Не удалось получить токен');
      return null;
    }
  } catch (error) {
    console.error('Ошибка при получении токена:', error);
    return null;
  }
};

export const onMessageListener = (callback: (payload: any) => void) => {
  onMessage(messaging, (payload) => {
    console.log("Получено сообщение:", payload);
    callback(payload);
  });
};

export const signInWithGoogle = async (): Promise<User | null> => {
  try {
    const result = await signInWithPopup(auth, googleProvider);
    return result.user;
  } catch (error) {
    console.error("Ошибка входа через Google:", error);
    throw error;
  }
};

export const logout = async () => {
  await signOut(auth);
};
