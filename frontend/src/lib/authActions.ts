/**
 * Bridge that lets non-React code (e.g. the axios interceptor) trigger a real Auth0 sign-out.
 * `Auth0SessionBridge` registers the actual handler once it has the `logout` function from the
 * Auth0 hook. A module-level guard prevents redundant/looping logout calls.
 */
type SessionExpiredHandler = () => void;

let handler: SessionExpiredHandler | null = null;
let handling = false;

export function registerSessionExpiredHandler(fn: SessionExpiredHandler): void {
  handler = fn;
}

/**
 * Called when the session is no longer valid (an API 401, or a failed silent token renewal).
 * Ends the Auth0 session and sends the user to /login. Runs at most once until a full reload.
 */
export function handleSessionExpired(): void {
  if (handling) return;
  // Already on the login flow — nothing to do.
  if (window.location.pathname.startsWith('/login')) return;
  handling = true;

  if (handler) {
    handler();
  } else {
    // Fallback if the bridge hasn't registered yet: hard redirect.
    window.location.assign('/login');
  }
}
