export const tokenStorageKey = "poolpredict.accessToken";
export const authChangedEventName = "poolpredict.authChanged";

export type UserProfile = {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  role: string;
  isEmailVerified: boolean;
  mustChangePassword: boolean;
};

export type AuthResponse = {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
};

export function getStoredToken() {
  return window.localStorage.getItem(tokenStorageKey);
}

export function storeToken(token: string) {
  window.localStorage.setItem(tokenStorageKey, token);
  dispatchAuthChanged();
}

export function clearToken() {
  window.localStorage.removeItem(tokenStorageKey);
  dispatchAuthChanged();
}

export function subscribeToAuthChanges(callback: () => void) {
  window.addEventListener(authChangedEventName, callback);
  return () => window.removeEventListener(authChangedEventName, callback);
}

function dispatchAuthChanged() {
  window.dispatchEvent(new Event(authChangedEventName));
}
