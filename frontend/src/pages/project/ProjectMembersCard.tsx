import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, UserPlus, Trash2, Users } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { useConfirm } from '../../components/ui/confirm-dialog';
import {
  listProjectMembers, addProjectMember, updateProjectMemberRole, removeProjectMember, getProject,
} from '../../api/projects';
import { listUsers, getMyProfile } from '../../api/users';
import { getErrorMessage } from '../../lib/apiClient';

const roles = ['Viewer', 'Editor', 'Owner'];

export function ProjectMembersCard({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();

  const membersQuery = useQuery({
    queryKey: ['project-members', projectId],
    queryFn: () => listProjectMembers(projectId),
  });
  const { data: workspaceUsers = [] } = useQuery({ queryKey: ['users'], queryFn: listUsers });
  const { data: me } = useQuery({ queryKey: ['me'], queryFn: getMyProfile });
  const { data: project } = useQuery({ queryKey: ['project', projectId], queryFn: () => getProject(projectId) });

  const members = membersQuery.data ?? [];
  const canManage = project?.currentUserRole === 'Owner' || me?.role === 'TenantAdmin' || me?.role === 'PlatformAdmin';

  const availableUsers = useMemo(() => {
    const memberIds = new Set(members.map((m) => m.userId));
    return workspaceUsers.filter((u) => !memberIds.has(u.id));
  }, [members, workspaceUsers]);

  const [addUserId, setAddUserId] = useState('');
  const [addRole, setAddRole] = useState('Viewer');

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['project-members', projectId] });

  const addMutation = useMutation({
    mutationFn: () => addProjectMember(projectId, addUserId, addRole),
    onSuccess: () => {
      invalidate();
      setAddUserId('');
      setAddRole('Viewer');
    },
  });

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) =>
      updateProjectMemberRole(projectId, userId, role),
    onSuccess: invalidate,
  });

  const removeMutation = useMutation({
    mutationFn: (userId: string) => removeProjectMember(projectId, userId),
    onSuccess: invalidate,
  });

  const mutationError =
    (addMutation.error && getErrorMessage(addMutation.error)) ||
    (roleMutation.error && getErrorMessage(roleMutation.error)) ||
    (removeMutation.error && getErrorMessage(removeMutation.error)) ||
    null;

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base flex items-center gap-2">
          <Users className="h-4 w-4 text-violet-600" /> Members
        </CardTitle>
        <CardDescription>
          Who can access this project and what they can do.
          {!canManage && ' Only project owners and workspace admins can make changes.'}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {mutationError && <Alert severity="error" className="text-sm">{mutationError}</Alert>}

        {canManage && (
          <div className="flex flex-wrap items-end gap-2">
            <div className="flex-1 min-w-[220px]">
              <Select value={addUserId} onValueChange={setAddUserId} disabled={availableUsers.length === 0}>
                <SelectTrigger className="h-9">
                  <SelectValue placeholder={availableUsers.length ? 'Select a workspace user…' : 'Everyone is already a member'} />
                </SelectTrigger>
                <SelectContent>
                  {availableUsers.map((u) => (
                    <SelectItem key={u.id} value={u.id}>
                      {u.displayName} · {u.email}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Select value={addRole} onValueChange={setAddRole}>
              <SelectTrigger className="h-9 w-[130px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {roles.map((r) => (
                  <SelectItem key={r} value={r}>{r}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              className="h-9"
              onClick={() => addMutation.mutate()}
              disabled={!addUserId || addMutation.isPending}
            >
              {addMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <UserPlus className="h-4 w-4 mr-1.5" />}
              Add
            </Button>
          </div>
        )}

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Email</TableHead>
              <TableHead>Role</TableHead>
              {canManage && <TableHead className="text-right">Actions</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {membersQuery.isLoading && (
              <TableRow>
                <TableCell colSpan={canManage ? 4 : 3} className="text-center py-8 text-muted-foreground text-sm">
                  <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading members…
                </TableCell>
              </TableRow>
            )}
            {!membersQuery.isLoading && members.length === 0 && (
              <TableRow>
                <TableCell colSpan={canManage ? 4 : 3} className="text-center py-8 text-muted-foreground text-sm">
                  No members yet.
                </TableCell>
              </TableRow>
            )}
            {members.map((m) => (
              <TableRow key={m.userId}>
                <TableCell className="font-medium">{m.displayName}</TableCell>
                <TableCell className="text-muted-foreground">{m.email}</TableCell>
                <TableCell>
                  {canManage ? (
                    <Select
                      value={m.role}
                      onValueChange={(role) => roleMutation.mutate({ userId: m.userId, role })}
                      disabled={roleMutation.isPending}
                    >
                      <SelectTrigger className="h-8 w-[120px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {roles.map((r) => (
                          <SelectItem key={r} value={r}>{r}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    <Badge variant="outline">{m.role}</Badge>
                  )}
                </TableCell>
                {canManage && (
                  <TableCell className="text-right">
                    <Button
                      size="icon"
                      variant="ghost"
                      className="h-8 w-8 text-muted-foreground hover:text-destructive"
                      aria-label={`Remove ${m.displayName}`}
                      disabled={removeMutation.isPending}
                      onClick={async () => {
                        const ok = await confirm({
                          title: 'Remove member?',
                          description: `${m.displayName} will lose access to this project.`,
                          confirmText: 'Remove',
                        });
                        if (ok) removeMutation.mutate(m.userId);
                      }}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
