import { useEffect } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { useAuthStore } from '../store/authStore';
import { getMyProfile, syncMyProfile } from '../api/users';
import { registerSessionExpiredHandler } from '../lib/authActions';

/**
 * Mirrors the Auth0 session into the app's auth store so existing code that reads `useAuthStore`
 * (the axios bearer token, signed image URLs, the top-bar user menu) keeps working without each
 * call site depending on Auth0 directly.
 */
export function Auth0SessionBridge() {
  const { isAuthenticated, isLoading, user, getAccessTokenSilently, logout } = useAuth0();

  // Let non-React code (the axios 401 interceptor) end the Auth0 session and return to /login.
  useEffect(() => {
    registerSessionExpiredHandler(() => {
      useAuthStore.getState().clear();
      logout({ logoutParams: { returnTo: `${window.location.origin}/login` } });
    });
  }, [logout]);

  useEffect(() => {
    let active = true;
    if (isLoading) return;

    if (isAuthenticated) {
      getAccessTokenSilently()
        .then(async (token) => {
          if (!active) return;
          // Seed the store with the token (needed for the API call below) and a provisional
          // profile from the Auth0 ID token.
          useAuthStore.getState().setSession({
            accessToken: token,
            expiresAtUtc: new Date(Date.now() + 3600 * 1000).toISOString(),
            userId: user?.sub ?? '',
            tenantId: '',
            email: user?.email ?? '',
            displayName: user?.name ?? user?.email ?? 'User',
            systemRole: '',
          });

          // The access token often omits email/name — push the ID-token profile so the backend
          // can heal the synthetic placeholder created during JIT provisioning. Either way, use
          // the authoritative DB record (which includes any name the user set on their Profile)
          // as the source of truth for the top-bar user menu.
          try {
            const profile = (user?.email || user?.name)
              ? await syncMyProfile({ email: user?.email, name: user?.name })
              : await getMyProfile();
            if (!active) return;
            useAuthStore.setState((s) => ({
              user: s.user
                ? {
                    ...s.user,
                    id: profile.id,
                    email: profile.email,
                    displayName: profile.displayName,
                    systemRole: profile.role,
                    tenantId: profile.tenantId ?? s.user.tenantId,
                    tenantName: profile.tenantName ?? s.user.tenantName,
                  }
                : s.user,
            }));
          } catch {
            /* best effort — keep the provisional profile */
          }
        })
        .catch(() => {
          if (!active) return;
          // Silent renewal failed (expired/absent refresh session): don't leave the app in a
          // half-authenticated state — sign out cleanly so the user lands on /login.
          useAuthStore.getState().clear();
          if (!window.location.pathname.startsWith('/login')) {
            logout({ logoutParams: { returnTo: `${window.location.origin}/login` } });
          }
        });
    } else {
      useAuthStore.getState().clear();
    }

    return () => { active = false; };
  }, [isAuthenticated, isLoading, user, getAccessTokenSilently, logout]);

  return null;
}
