import { initializeApp } from "firebase/app";
import type { FirebaseApp } from "firebase/app";
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut } from "firebase/auth";
import type { Auth, User } from "firebase/auth";
import { getMessaging, getToken, onMessage } from "firebase/messaging";
import type { Messaging } from "firebase/messaging";

let app: FirebaseApp | null = null;
let auth: Auth | null = null;
let messaging: Messaging | null = null;
let googleProvider: GoogleAuthProvider | null = null;

const getFirebaseConfig = () => {
  const runtimeConfig = useRuntimeConfig();

  return {
    apiKey: runtimeConfig.public.firebaseApiKey,
    authDomain: runtimeConfig.public.firebaseAuthDomain,
    projectId: runtimeConfig.public.firebaseProjectId,
    storageBucket: runtimeConfig.public.firebaseStorageBucket,
    messagingSenderId: runtimeConfig.public.firebaseMessagingSenderId,
    appId: runtimeConfig.public.firebaseAppId,
    measurementId: runtimeConfig.public.firebaseMeasurementId,
  };
};

export const isFirebaseConfigured = () => {
  if (import.meta.server) return false;

  const config = getFirebaseConfig();
  return Boolean(config.apiKey && config.authDomain && config.projectId && config.appId);
};

const ensureFirebaseApp = () => {
  if (import.meta.server || !isFirebaseConfigured()) {
    return null;
  }

  if (!app) {
    app = initializeApp(getFirebaseConfig());
  }

  return app;
};

export const getFirebaseAuth = () => {
  const firebaseApp = ensureFirebaseApp();
  if (!firebaseApp) {
    throw new Error("Firebase is not configured for this environment.");
  }

  auth ??= getAuth(firebaseApp);
  return auth;
};

const getFirebaseMessaging = () => {
  const firebaseApp = ensureFirebaseApp();
  if (!firebaseApp) {
    return null;
  }

  messaging ??= getMessaging(firebaseApp);
  return messaging;
};

const getGoogleProvider = () => {
  googleProvider ??= new GoogleAuthProvider();
  return googleProvider;
};

export type AuthUser = User | null;

export const getFcmToken = async (): Promise<string | null> => {
  try {
    const messagingInstance = getFirebaseMessaging();
    if (!messagingInstance) return null;

    const runtimeConfig = useRuntimeConfig();
    const token = await getToken(messagingInstance, {
      vapidKey: runtimeConfig.public.firebaseVapidKey,
    });

    return token || null;
  } catch (error) {
    console.error("Error while getting FCM token:", error);
    return null;
  }
};

export const onMessageListener = (callback: (payload: any) => void) => {
  const messagingInstance = getFirebaseMessaging();
  if (!messagingInstance) return;

  onMessage(messagingInstance, (payload) => {
    callback(payload);
  });
};

export const signInWithGoogle = async (): Promise<User | null> => {
  try {
    const result = await signInWithPopup(getFirebaseAuth(), getGoogleProvider());
    return result.user;
  } catch (error) {
    console.error("Google sign-in error:", error);
    throw error;
  }
};

export const logout = async () => {
  if (import.meta.server) return;
  await signOut(getFirebaseAuth());
};
