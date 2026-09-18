import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth0 } from '@auth0/auth0-react';
import { ArrowRight, Loader2, ShieldCheck } from 'lucide-react';
import { AuthBrandPanel } from '../components/AuthBrandPanel';
import { AtipLogo, AtipWordmark } from '../components/AtipLogo';
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
    <div className="flex h-full bg-white">
      <AuthBrandPanel />

      <div className="flex flex-1 items-center justify-center p-6 sm:p-10">
        <div className="w-full max-w-sm">
          {/* The brand panel is hidden below md, so the small screen still needs a logo. */}
          <div className="mb-10 flex items-center gap-2.5 md:hidden">
            <AtipLogo size={36} />
            <AtipWordmark size={24} />
          </div>

          <h1 className="text-[26px] font-bold leading-tight text-foreground">Welcome back</h1>
          <p className="mt-1.5 text-sm text-muted-foreground">
            Sign in to your ATiP workspace to pick up where your last run left off.
          </p>

          <div className="mt-8">
            {isLoading ? (
              // Same height as the button, so the panel doesn't jump when the session check lands.
              <div className="flex h-11 items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Checking your session…
              </div>
            ) : (
              <Button className="group h-11 w-full text-[15px]" size="lg" onClick={signIn}>
                Continue with Auth0
                <ArrowRight className="ml-2 h-4 w-4 transition-transform group-hover:translate-x-0.5" />
              </Button>
            )}
          </div>

          <div className="mt-8 flex items-start gap-2.5 rounded-lg border border-border bg-muted/40 p-3">
            <ShieldCheck className="mt-px h-4 w-4 flex-shrink-0 text-violet-600" />
            <p className="text-xs leading-relaxed text-muted-foreground">
              Authentication is handled by Auth0. Single sign-on and multi-factor policies configured
              for your tenant are enforced automatically.
            </p>
          </div>

          <p className="mt-8 text-xs text-muted-foreground">
            Trouble signing in? Contact your workspace administrator.
          </p>
        </div>
      </div>
    </div>
  );
}
