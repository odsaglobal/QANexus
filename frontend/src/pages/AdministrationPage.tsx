import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Users, ScrollText, KeyRound, Plus, Trash2, Copy, Check } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Alert } from '../components/ui/alert';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '../components/ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { useConfirm } from '../components/ui/confirm-dialog';
import { UserAvatar } from '../components/UserAvatar';
import { getErrorMessage } from '../lib/apiClient';
import { listUsers, getMyProfile, changeUserSystemRole } from '../api/users';
import { listAuditLogs } from '../api/audit';
import { listApiKeys, createApiKey, revokeApiKey, type CreatedApiKey } from '../api/apiKeys';

function roleLabel(role: string): string {
  if (role === 'TenantAdmin') return 'Tenant Admin';
  if (role === 'PlatformAdmin') return 'Platform Admin';
  return role;
}

export function AdministrationPage() {
  const { data: users = [] } = useQuery({
    queryKey: ['users'],
    queryFn: listUsers,
  });

  const stats = useMemo(() => ({
    total: users.length,
    active: users.filter((u) => u.isActive).length,
    admins: users.filter((u) => u.role === 'TenantAdmin' || u.role === 'PlatformAdmin').length,
  }), [users]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Administration</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Manage workspace members, API keys and the activity trail for your tenant.</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Members</p><p className="text-3xl font-bold mt-1">{stats.total}</p></CardContent></Card>
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Active</p><p className="text-3xl font-bold mt-1">{stats.active}</p></CardContent></Card>
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Admins</p><p className="text-3xl font-bold mt-1">{stats.admins}</p></CardContent></Card>
      </div>

      <Tabs defaultValue="members">
        <TabsList>
          <TabsTrigger value="members"><Users className="h-4 w-4 mr-1.5" /> Members</TabsTrigger>
          <TabsTrigger value="api-keys"><KeyRound className="h-4 w-4 mr-1.5" /> API keys</TabsTrigger>
          <TabsTrigger value="activity"><ScrollText className="h-4 w-4 mr-1.5" /> Activity log</TabsTrigger>
        </TabsList>
        <TabsContent value="members" className="mt-4">
          <MembersCard />
        </TabsContent>
        <TabsContent value="api-keys" className="mt-4">
          <ApiKeysCard />
        </TabsContent>
        <TabsContent value="activity" className="mt-4">
          <ActivityLog />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function MembersCard() {
  const queryClient = useQueryClient();
  const { data: users = [], isLoading, isError, error } = useQuery({
    queryKey: ['users'],
    queryFn: listUsers,
  });
  const { data: me } = useQuery({ queryKey: ['me'], queryFn: getMyProfile });
  const isAdmin = me?.role === 'TenantAdmin' || me?.role === 'PlatformAdmin';

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) => changeUserSystemRole(userId, role),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });

  return (
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-semibold flex items-center gap-2"><Users className="h-4 w-4 text-violet-600" /> Workspace members</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {isError && <Alert severity="error" className="text-sm mb-3">{(error as Error)?.message ?? 'Failed to load users.'}</Alert>}
          {roleMutation.isError && <Alert severity="error" className="text-sm mb-3">{getErrorMessage(roleMutation.error)}</Alert>}
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Sign-in</TableHead>
                <TableHead>Last login</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-10 text-muted-foreground text-sm">
                    <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading members…
                  </TableCell>
                </TableRow>
              )}
              {!isLoading && users.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-10 text-muted-foreground text-sm">
                    No members yet.
                  </TableCell>
                </TableRow>
              )}
              {users.map((u) => {
                // Platform admins aren't editable here; only Member ↔ TenantAdmin.
                const editable = isAdmin && u.role !== 'PlatformAdmin';
                return (
                  <TableRow key={u.id}>
                    <TableCell>
                      <div className="flex items-center gap-2.5">
                        <UserAvatar name={u.displayName} email={u.email} size={32} />
                        <span className="font-medium">{u.displayName}</span>
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground">{u.email}</TableCell>
                    <TableCell>
                      {editable ? (
                        <Select
                          value={u.role}
                          onValueChange={(role) => roleMutation.mutate({ userId: u.id, role })}
                          disabled={roleMutation.isPending}
                        >
                          <SelectTrigger className="h-8 w-[150px]">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Member">Member</SelectItem>
                            <SelectItem value="TenantAdmin">Tenant Admin</SelectItem>
                          </SelectContent>
                        </Select>
                      ) : (
                        <Badge variant="outline">{roleLabel(u.role)}</Badge>
                      )}
                    </TableCell>
                    <TableCell><Badge variant="secondary">{u.isFederated ? 'SSO' : 'Local'}</Badge></TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {u.lastLoginAtUtc ? new Date(u.lastLoginAtUtc).toLocaleString() : '—'}
                    </TableCell>
                    <TableCell><Badge variant={u.isActive ? 'success' : 'warning'}>{u.isActive ? 'Active' : 'Disabled'}</Badge></TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
  );
}

function ApiKeysCard() {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [createOpen, setCreateOpen] = useState(false);
  const [name, setName] = useState('');
  const [created, setCreated] = useState<CreatedApiKey | null>(null);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { data: keys = [], isLoading } = useQuery({
    queryKey: ['api-keys'],
    queryFn: listApiKeys,
  });

  const createMutation = useMutation({
    mutationFn: () => createApiKey(name.trim()),
    onSuccess: (result) => {
      setError(null);
      setCreated(result);
      setName('');
      setCreateOpen(false);
      queryClient.invalidateQueries({ queryKey: ['api-keys'] });
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const revokeMutation = useMutation({
    mutationFn: revokeApiKey,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['api-keys'] }),
    onError: (e) => setError(getErrorMessage(e)),
  });

  const copyKey = () => {
    if (!created) return;
    navigator.clipboard.writeText(created.plainText).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  };

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-semibold flex items-center gap-2">
            <KeyRound className="h-4 w-4 text-violet-600" /> API keys
            <span className="text-xs font-normal text-muted-foreground">— for CI systems (X-Api-Key header)</span>
          </CardTitle>
          <Button size="sm" onClick={() => { setError(null); setCreateOpen(true); }}>
            <Plus className="h-4 w-4 mr-1.5" /> New key
          </Button>
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        {error && <Alert severity="error" className="text-sm mb-3">{error}</Alert>}
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Key</TableHead>
              <TableHead>Last used</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow><TableCell colSpan={5} className="text-center py-8 text-muted-foreground text-sm">
                <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading…
              </TableCell></TableRow>
            )}
            {!isLoading && keys.length === 0 && (
              <TableRow><TableCell colSpan={5} className="text-center py-8 text-muted-foreground text-sm">
                No API keys yet. Create one to trigger runs from a pipeline.
              </TableCell></TableRow>
            )}
            {keys.map((k) => (
              <TableRow key={k.id}>
                <TableCell className="font-medium">{k.name}</TableCell>
                <TableCell><code className="text-xs bg-muted px-1.5 py-0.5 rounded">{k.prefix}…</code></TableCell>
                <TableCell className="text-xs text-muted-foreground">
                  {k.lastUsedAtUtc ? new Date(k.lastUsedAtUtc).toLocaleString() : 'never'}
                </TableCell>
                <TableCell><Badge variant={k.isActive ? 'success' : 'secondary'}>{k.isActive ? 'Active' : 'Revoked'}</Badge></TableCell>
                <TableCell className="text-right">
                  {k.isActive && (
                    <Button
                      variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive"
                      aria-label={`Revoke ${k.name}`}
                      onClick={async () => {
                        if (await confirm({ title: 'Revoke API key?', description: `"${k.name}" will stop working immediately.`, confirmText: 'Revoke', tone: 'destructive' })) {
                          revokeMutation.mutate(k.id);
                        }
                      }}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>

      {/* Create dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>Create API key</DialogTitle></DialogHeader>
          <div className="space-y-3 py-1">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="GitHub Actions" autoFocus />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button disabled={!name.trim() || createMutation.isPending} onClick={() => createMutation.mutate()}>
              {createMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* One-time secret reveal */}
      <Dialog open={!!created} onOpenChange={(o) => { if (!o) setCreated(null); }}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Copy your API key</DialogTitle></DialogHeader>
          <div className="space-y-3 py-1">
            <Alert severity="warning" className="text-sm">
              This is the only time the key is shown. Store it securely.
            </Alert>
            <div className="flex items-center gap-2">
              <code className="flex-1 text-xs bg-muted px-2 py-2 rounded break-all">{created?.plainText}</code>
              <Button size="icon" variant="outline" className="h-9 w-9 shrink-0" onClick={copyKey} aria-label="Copy">
                {copied ? <Check className="h-4 w-4 text-green-600" /> : <Copy className="h-4 w-4" />}
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Use it as a header: <code className="bg-muted px-1 rounded">X-Api-Key: {created?.plainText.slice(0, 12)}…</code>
            </p>
          </div>
          <DialogFooter>
            <Button onClick={() => setCreated(null)}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

const categoryColor: Record<string, string> = {
  Project: 'bg-blue-100 text-blue-700',
  Scenario: 'bg-violet-100 text-violet-700',
  Suite: 'bg-teal-100 text-teal-700',
  Auth: 'bg-amber-100 text-amber-700',
};

function ActivityLog() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['audit-logs', { page: 1, pageSize: 50 }],
    queryFn: () => listAuditLogs({ page: 1, pageSize: 50 }),
  });
  const entries = data?.items ?? [];

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center gap-2">
          <ScrollText className="h-4 w-4 text-violet-600" /> Activity log
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-0">
        {isError && <Alert severity="error" className="text-sm mb-3">{(error as Error)?.message ?? 'Failed to load activity.'}</Alert>}
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>When</TableHead>
              <TableHead>User</TableHead>
              <TableHead>Category</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>IP</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading && (
              <TableRow>
                <TableCell colSpan={5} className="text-center py-10 text-muted-foreground text-sm">
                  <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading activity…
                </TableCell>
              </TableRow>
            )}
            {!isLoading && entries.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center py-10 text-muted-foreground text-sm">
                  No activity recorded yet.
                </TableCell>
              </TableRow>
            )}
            {entries.map((e) => (
              <TableRow key={e.id}>
                <TableCell className="text-xs text-muted-foreground whitespace-nowrap">{new Date(e.timestampUtc).toLocaleString()}</TableCell>
                <TableCell className="text-xs">{e.userEmail ?? 'system'}</TableCell>
                <TableCell>
                  <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${categoryColor[e.category] ?? 'bg-gray-100 text-gray-700'}`}>
                    {e.category}
                  </span>
                </TableCell>
                <TableCell className="text-sm">{e.summary}</TableCell>
                <TableCell className="text-xs text-muted-foreground">{e.ipAddress ?? '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
