import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResult } from '../api/types';

interface AuthState {
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: {
    id: string;
    email: string;
    displayName: string;
    tenantId: string;
    systemRole: string;
  } | null;
  setSession: (result: AuthResult) => void;
  clear: () => void;
  isAuthenticated: () => boolean;
}

/** Auth session persisted to localStorage so a refresh keeps the user signed in. */
export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      expiresAtUtc: null,
      user: null,
      setSession: (result) =>
        set({
          accessToken: result.accessToken,
          expiresAtUtc: result.expiresAtUtc,
          user: {
            id: result.userId,
            email: result.email,
            displayName: result.displayName,
            tenantId: result.tenantId,
            systemRole: result.systemRole,
          },
        }),
      clear: () => set({ accessToken: null, expiresAtUtc: null, user: null }),
      isAuthenticated: () => {
        const { accessToken, expiresAtUtc } = get();
        if (!accessToken || !expiresAtUtc) {
          return false;
        }
        return new Date(expiresAtUtc).getTime() > Date.now();
      },
    }),
    { name: 'atip-auth' },
  ),
);
