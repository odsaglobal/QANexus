import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth0 } from '@auth0/auth0-react';
import { Loader2, LogIn } from 'lucide-react';
import { AuthBrandPanel } from '../components/AuthBrandPanel';
import { Button } from '../components/ui/button';

/** Landing sign-in page. Stays put until the user chooses to continue to Auth0. */
export function LoginPage() {
  const { loginWithRedirect, isAuthenticated, isLoading } = useAuth0();
  const navigate = useNavigate();
  const location = useLocation();
  const returnTo = (location.state as { from?: string } | null)?.from ?? '/';

  // If already signed in, don't show the login page — go to the app.
  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      navigate('/', { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate]);

  const signIn = () => loginWithRedirect({ appState: { returnTo } });

  return (
    <div className="flex h-full bg-gray-50">
      <AuthBrandPanel />
      <div className="flex-1 flex items-center justify-center p-6 bg-white">
        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-foreground mb-1">Welcome back</h1>
            <p className="text-muted-foreground text-sm">Sign in to your QANexus workspace.</p>
          </div>

          {isLoading ? (
            <p className="text-muted-foreground text-sm flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" /> Checking your session…
            </p>
          ) : (
            <Button className="w-full" size="lg" onClick={signIn}>
              <LogIn className="h-4 w-4 mr-2" /> Continue with Auth0
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
