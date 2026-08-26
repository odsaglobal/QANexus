import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Users } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Alert } from '../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { listUsers } from '../api/users';

function roleLabel(role: string): string {
  if (role === 'TenantAdmin') return 'Tenant Admin';
  if (role === 'PlatformAdmin') return 'Platform Admin';
  return role;
}

function initials(name: string): string {
  return name
    .split(' ')
    .map((p) => p[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

export function AdministrationPage() {
  const { data: users = [], isLoading, isError, error } = useQuery({
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
          <p className="text-sm text-muted-foreground mt-0.5">Workspace members provisioned in your tenant. Users are created automatically on their first sign-in.</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Members</p><p className="text-3xl font-bold mt-1">{stats.total}</p></CardContent></Card>
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Active</p><p className="text-3xl font-bold mt-1">{stats.active}</p></CardContent></Card>
        <Card><CardContent className="pt-6"><p className="text-sm text-muted-foreground">Admins</p><p className="text-3xl font-bold mt-1">{stats.admins}</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-semibold flex items-center gap-2"><Users className="h-4 w-4 text-violet-600" /> Workspace members</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {isError && <Alert severity="error" className="text-sm mb-3">{(error as Error)?.message ?? 'Failed to load users.'}</Alert>}
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
              {users.map((u) => (
                <TableRow key={u.id}>
                  <TableCell>
                    <div className="flex items-center gap-2.5">
                      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-violet-100 text-xs font-semibold text-violet-700">
                        {initials(u.displayName)}
                      </span>
                      <span className="font-medium">{u.displayName}</span>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{u.email}</TableCell>
                  <TableCell><Badge variant="outline">{roleLabel(u.role)}</Badge></TableCell>
                  <TableCell><Badge variant="secondary">{u.isFederated ? 'SSO' : 'Local'}</Badge></TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {u.lastLoginAtUtc ? new Date(u.lastLoginAtUtc).toLocaleString() : '—'}
                  </TableCell>
                  <TableCell><Badge variant={u.isActive ? 'success' : 'warning'}>{u.isActive ? 'Active' : 'Disabled'}</Badge></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
