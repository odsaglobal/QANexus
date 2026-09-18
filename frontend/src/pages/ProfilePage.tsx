import { useEffect, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth0 } from '@auth0/auth0-react';
import { Loader2, Mail, Shield, Clock, CalendarDays, Fingerprint, CheckCircle2, XCircle, Save, Building2 } from 'lucide-react';
import { PageHeader } from '../components/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Badge } from '../components/ui/badge';
import { UserAvatar } from '../components/UserAvatar';
import { Separator } from '../components/ui/separator';
import { getMyProfile, updateMyDisplayName } from '../api/users';
import { useAuthStore } from '../store/authStore';
import { getErrorMessage } from '../lib/apiClient';

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function ProfilePage() {
  const queryClient = useQueryClient();
  const { user: auth0User } = useAuth0();

  const profileQuery = useQuery({
    queryKey: ['me'],
    queryFn: getMyProfile,
  });

  const profile = profileQuery.data;
  const [name, setName] = useState('');

  useEffect(() => {
    if (profile) setName(profile.displayName);
  }, [profile]);

  const saveMutation = useMutation({
    mutationFn: (value: string) => updateMyDisplayName(value),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ['me'] });
      // Keep the top-bar user menu in sync immediately.
      useAuthStore.setState((s) => ({
        user: s.user ? { ...s.user, displayName: updated.displayName } : s.user,
      }));
    },
  });

  const trimmed = name.trim();
  const dirty = Boolean(profile) && trimmed.length > 0 && trimmed !== profile?.displayName;

  if (profileQuery.isLoading) {
    return (
      <div className="flex h-64 items-center justify-center text-muted-foreground">
        <Loader2 className="h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (profileQuery.isError || !profile) {
    return (
      <div className="space-y-6">
        <PageHeader title="Profile" />
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            Could not load your profile. {getErrorMessage(profileQuery.error)}
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Profile" description="Your account details and preferences." />

      {/* Identity header */}
      <Card>
        <CardContent className="flex items-center gap-4 p-6">
          <UserAvatar
            name={profile.displayName}
            email={profile.email}
            imageUrl={auth0User?.picture}
            size={64}
          />
          <div className="min-w-0">
            <h2 className="text-xl font-bold text-foreground truncate">{profile.displayName}</h2>
            <p className="text-sm text-muted-foreground truncate">{profile.email}</p>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <Badge variant="secondary" className="gap-1">
                <Shield className="h-3 w-3" /> {profile.role}
              </Badge>
              {profile.isActive ? (
                <Badge variant="secondary" className="gap-1 text-green-700">
                  <CheckCircle2 className="h-3 w-3" /> Active
                </Badge>
              ) : (
                <Badge variant="secondary" className="gap-1 text-red-700">
                  <XCircle className="h-3 w-3" /> Inactive
                </Badge>
              )}
              <Badge variant="outline">{profile.isFederated ? 'Federated (SSO)' : 'Local account'}</Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Editable details */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Account details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-2 max-w-md">
            <Label htmlFor="displayName">Display name</Label>
            <Input
              id="displayName"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Your name"
              maxLength={120}
            />
          </div>

          <div className="grid gap-2 max-w-md">
            <Label htmlFor="email" className="flex items-center gap-1.5">
              <Mail className="h-3.5 w-3.5 text-muted-foreground" /> Email
            </Label>
            <Input id="email" value={profile.email} disabled readOnly />
            <p className="text-xs text-muted-foreground">
              Email is managed by your identity provider and can't be changed here.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Button
              onClick={() => saveMutation.mutate(trimmed)}
              disabled={!dirty || saveMutation.isPending}
              className="h-9"
            >
              {saveMutation.isPending ? (
                <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />
              ) : (
                <Save className="h-4 w-4 mr-1.5" />
              )}
              Save changes
            </Button>
            {dirty && !saveMutation.isPending && (
              <Button variant="ghost" className="h-9" onClick={() => setName(profile.displayName)}>
                Reset
              </Button>
            )}
            {saveMutation.isSuccess && !dirty && (
              <span className="text-sm text-green-600 flex items-center gap-1">
                <CheckCircle2 className="h-4 w-4" /> Saved
              </span>
            )}
            {saveMutation.isError && (
              <span className="text-sm text-destructive">{getErrorMessage(saveMutation.error)}</span>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Read-only metadata */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Account information</CardTitle>
        </CardHeader>
        <CardContent className="divide-y divide-border">
          <InfoRow icon={Building2} label="Workspace" value={profile.tenantName ?? '—'} />
          <InfoRow icon={Shield} label="Role" value={profile.role} />
          <Separator className="my-0" />
          <InfoRow icon={Clock} label="Last sign-in" value={formatDate(profile.lastLoginAtUtc)} />
          <InfoRow icon={CalendarDays} label="Member since" value={formatDate(profile.createdAtUtc)} />
          <InfoRow icon={Fingerprint} label="User ID" value={profile.id} mono />
          {auth0User?.sub && (
            <InfoRow icon={Fingerprint} label="Identity provider ID" value={auth0User.sub} mono />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function InfoRow({
  icon: Icon,
  label,
  value,
  mono,
}: {
  icon: typeof Shield;
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-center justify-between gap-4 py-3">
      <span className="flex items-center gap-2 text-sm text-muted-foreground">
        <Icon className="h-4 w-4 text-gray-400" /> {label}
      </span>
      <span className={`text-sm text-foreground truncate ${mono ? 'font-mono text-xs' : ''}`}>{value}</span>
    </div>
  );
}
