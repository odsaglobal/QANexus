/**
 * Auth0 SPA configuration. Values come from Vite env vars so they can differ per environment;
 * sensible defaults match the configured Auth0 tenant. See frontend/.env.example.
 *   VITE_AUTH0_DOMAIN    e.g. dev-abc123.us.auth0.com
 *   VITE_AUTH0_CLIENT_ID the SPA application's client id
 *   VITE_AUTH0_AUDIENCE  the API Identifier (Applications → APIs), e.g. https://qanexus-api
 */
export const auth0Domain =
  (import.meta.env.VITE_AUTH0_DOMAIN as string | undefined) ?? 'dev-igvssogoc8glgn1c.us.auth0.com';

export const auth0ClientId =
  (import.meta.env.VITE_AUTH0_CLIENT_ID as string | undefined) ?? 'ZOnXv00I6xizUdkJeU50eWDcHap7YcA6';

export const auth0Audience =
  (import.meta.env.VITE_AUTH0_AUDIENCE as string | undefined) ?? 'https://qanexus-api';

export const auth0RedirectUri = `${window.location.origin}/login/callback`;
