import { useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { Eye, EyeOff, Loader2 } from 'lucide-react';
import { login } from '../api/auth';
import { getErrorMessage } from '../lib/apiClient';
import { useAuthStore } from '../store/authStore';
import { AuthBrandPanel } from '../components/AuthBrandPanel';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Alert } from '../components/ui/alert';

export function LoginPage() {
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPwd, setShowPwd] = useState(false);

  const mutation = useMutation({
    mutationFn: () => login({ email, password }),
    onSuccess: (result) => {
      setSession(result);
      navigate('/', { replace: true });
    },
  });

  return (
    <div className="flex h-full bg-gray-50">
      <AuthBrandPanel />
      <div className="flex-1 flex items-center justify-center p-6 bg-white">
        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-foreground mb-1">Welcome back</h1>
            <p className="text-muted-foreground text-sm">Sign in to your ATiP workspace.</p>
          </div>

          <form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }} className="space-y-4">
            {mutation.isError && (
              <Alert severity="error">{getErrorMessage(mutation.error)}</Alert>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" placeholder="you@company.com" value={email}
                onChange={(e) => setEmail(e.target.value)} required />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="password">Password</Label>
              <div className="relative">
                <Input id="password" type={showPwd ? 'text' : 'password'} placeholder="••••••••"
                  value={password} onChange={(e) => setPassword(e.target.value)} required className="pr-10" />
                <button type="button" onClick={() => setShowPwd(!showPwd)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition-colors">
                  {showPwd ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <Button type="submit" className="w-full" disabled={mutation.isPending}>
              {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {mutation.isPending ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-muted-foreground">
            No account yet?{' '}
            <RouterLink to="/register" className="text-violet-600 font-semibold hover:underline">
              Create an organization
            </RouterLink>
          </p>
        </div>
      </div>
    </div>
  );
}
