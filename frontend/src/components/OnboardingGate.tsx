import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, ArrowRight } from 'lucide-react';
import { getMyProfile } from '../api/users';
import { updateTenant } from '../api/tenant';
import { useAuthStore } from '../store/authStore';
import { getErrorMessage } from '../lib/apiClient';
import { AtipLogo, AtipWordmark } from './AtipLogo';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';

const placeholderNames = ['Users', 'My Workspace', 'Workspace'];

/**
 * First-run onboarding. When the signed-in owner's workspace hasn't been named yet, this blocks
 * the app with a "Name your workspace" step (the B2B SaaS convention). Only shown to the tenant
 * admin (the workspace creator); everyone else just proceeds.
 */
export function OnboardingGate() {
  const queryClient = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ['me'], queryFn: getMyProfile });

  const needsOnboarding =
    !!data && data.tenantIsOnboarded === false && data.role === 'TenantAdmin';

  const [name, setName] = useState('');

  useEffect(() => {
    if (!data || !needsOnboarding) return;
    const suggested =
      data.tenantName && !placeholderNames.includes(data.tenantName)
        ? data.tenantName
        : data.displayName && !data.displayName.includes('@')
          ? `${data.displayName.split(' ')[0]}'s Workspace`
          : '';
    setName(suggested);
  }, [data, needsOnboarding]);

  const mutation = useMutation({
    mutationFn: (value: string) => updateTenant(value),
    onSuccess: (tenant) => {
      useAuthStore.setState((s) => ({
        user: s.user ? { ...s.user, tenantName: tenant.name } : s.user,
      }));
      queryClient.invalidateQueries({ queryKey: ['me'] });
    },
  });

  if (isLoading || !needsOnboarding) return null;

  const trimmed = name.trim();

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-white p-6">
      <div className="w-full max-w-md">
        <div className="flex items-center gap-3 mb-8">
          <AtipLogo size={40} />
          <AtipWordmark size={26} />
        </div>

        <h1 className="text-2xl font-bold text-foreground">Name your workspace</h1>
        <p className="text-muted-foreground mt-1.5">
          This is your team's home in ATiP — projects, scenarios, and runs live here. You can
          change it later in Settings.
        </p>

        <form
          className="mt-8 space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            if (trimmed) mutation.mutate(trimmed);
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="workspaceName">Workspace name</Label>
            <Input
              id="workspaceName"
              autoFocus
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Acme QA"
              maxLength={200}
              className="h-11 text-base"
            />
          </div>

          {mutation.isError && (
            <p className="text-sm text-destructive">{getErrorMessage(mutation.error)}</p>
          )}

          <Button
            type="submit"
            disabled={!trimmed || mutation.isPending}
            className="h-11 w-full text-base"
          >
            {mutation.isPending ? (
              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            ) : (
              <ArrowRight className="h-4 w-4 mr-2" />
            )}
            Continue
          </Button>
        </form>
      </div>
    </div>
  );
}
