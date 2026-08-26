import { useEffect } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { useAuthStore } from '../store/authStore';
import { syncMyProfile } from '../api/users';

/**
 * Mirrors the Auth0 session into the app's auth store so existing code that reads `useAuthStore`
 * (the axios bearer token, signed image URLs, the top-bar user menu) keeps working without each
 * call site depending on Auth0 directly.
 */
export function Auth0SessionBridge() {
  const { isAuthenticated, isLoading, user, getAccessTokenSilently } = useAuth0();

  useEffect(() => {
    let active = true;
    if (isLoading) return;

    if (isAuthenticated) {
      getAccessTokenSilently()
        .then((token) => {
          if (!active) return;
          useAuthStore.getState().setSession({
            accessToken: token,
            expiresAtUtc: new Date(Date.now() + 3600 * 1000).toISOString(),
            userId: user?.sub ?? '',
            tenantId: '',
            email: user?.email ?? '',
            displayName: user?.name ?? user?.email ?? 'User',
            systemRole: '',
          });
          // The access token often omits email/name — push the ID-token profile so the
          // backend can replace the synthetic placeholder created during JIT provisioning.
          if (user?.email || user?.name) {
            syncMyProfile({ email: user?.email, name: user?.name }).catch(() => { /* best effort */ });
          }
        })
        .catch(() => { /* token retrieval failed; leave store cleared */ });
    } else {
      useAuthStore.getState().clear();
    }

    return () => { active = false; };
  }, [isAuthenticated, isLoading, user, getAccessTokenSilently]);

  return null;
}
