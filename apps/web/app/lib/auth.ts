export const tokenStorageKey = "poolpredict.accessToken";

export type UserProfile = {
  id: string;
  email: string;
  displayName: string;
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
}

export function clearToken() {
  window.localStorage.removeItem(tokenStorageKey);
}
