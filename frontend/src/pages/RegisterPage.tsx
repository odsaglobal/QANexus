import { useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { register } from '../api/auth';
import { getErrorMessage } from '../lib/apiClient';
import { useAuthStore } from '../store/authStore';
import { AuthBrandPanel } from '../components/AuthBrandPanel';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Alert } from '../components/ui/alert';

export function RegisterPage() {
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const [form, setForm] = useState({ organizationName: '', displayName: '', email: '', password: '' });

  const update = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((prev) => ({ ...prev, [key]: e.target.value }));

  const mutation = useMutation({
    mutationFn: () => register(form),
    onSuccess: (result) => {
      setSession(result);
      navigate('/', { replace: true });
    },
  });

  return (
    <div className="flex h-full bg-gray-50">
      <AuthBrandPanel />
      <div className="flex-1 flex items-center justify-center p-6 bg-white overflow-y-auto">
        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-foreground mb-1">Create your organization</h1>
            <p className="text-muted-foreground text-sm">You'll be the first administrator.</p>
          </div>

          <form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }} className="space-y-4">
            {mutation.isError && (
              <Alert severity="error">{getErrorMessage(mutation.error)}</Alert>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="orgName">Organization name</Label>
              <Input id="orgName" placeholder="Acme Corp" value={form.organizationName} onChange={update('organizationName')} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="displayName">Your name</Label>
              <Input id="displayName" placeholder="Jane Smith" value={form.displayName} onChange={update('displayName')} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="email">Work email</Label>
              <Input id="email" type="email" placeholder="jane@acme.com" value={form.email} onChange={update('email')} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="password">Password</Label>
              <Input id="password" type="password" placeholder="Min. 8 characters" value={form.password} onChange={update('password')} required />
            </div>

            <Button type="submit" className="w-full" disabled={mutation.isPending}>
              {mutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {mutation.isPending ? 'Creating…' : 'Create organization'}
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-muted-foreground">
            Already have an account?{' '}
            <RouterLink to="/login" className="text-violet-600 font-semibold hover:underline">
              Sign in
            </RouterLink>
          </p>
        </div>
      </div>
    </div>
  );
}
