import { initializeApp } from "firebase/app";
import { getAuth, GoogleAuthProvider, signInWithPopup } from "firebase/auth";
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

// Initialize Firebase
const app = initializeApp(firebaseConfig);

// Initialize Messaging
const messaging = getMessaging(app);

// Initialize Auth
const auth = getAuth(app);
const provider = new GoogleAuthProvider();

/**
 * Запрос разрешений на уведомления
 */
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

/**
 * Подписка на входящие сообщения
 * @param callback - Функция для обработки сообщений
 */
export const onMessageListener = (callback: (payload: any) => void) => {
  onMessage(messaging, (payload) => {
    console.log("Получено сообщение:", payload);
    callback(payload);
  });
};

/**
 * Sign in with Google
 */
export const signInWithGoogle = async (): Promise<User | null> => {
  try {
    const result = await signInWithPopup(auth, provider);
    console.log('User signed in: ', result.user);
    return result.user;
  } catch (error) {
    console.error('Error signing in: ', error);
    return null;
  }
};
