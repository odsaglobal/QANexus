import { StrictMode, useCallback } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, useNavigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Auth0Provider, type AppState } from '@auth0/auth0-react';
import { App } from './App';
import { ConfirmProvider } from './components/ui/confirm-dialog';
import { Auth0SessionBridge } from './components/Auth0SessionBridge';
import { auth0Audience, auth0ClientId, auth0Domain, auth0RedirectUri } from './lib/auth0';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
});

function Root() {
  const navigate = useNavigate();
  const onRedirectCallback = useCallback(
    (appState?: AppState) => {
      navigate(appState?.returnTo ?? '/', { replace: true });
    },
    [navigate],
  );

  return (
    <Auth0Provider
      domain={auth0Domain}
      clientId={auth0ClientId}
      authorizationParams={{
        redirect_uri: auth0RedirectUri,
        audience: auth0Audience,
        scope: 'openid profile email',
      }}
      onRedirectCallback={onRedirectCallback}
      cacheLocation="localstorage"
      useRefreshTokens
    >
      <Auth0SessionBridge />
      <ConfirmProvider>
        <App />
      </ConfirmProvider>
    </Auth0Provider>
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Root />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
