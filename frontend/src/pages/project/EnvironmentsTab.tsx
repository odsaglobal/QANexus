import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, ExternalLink, Loader2, Database, KeyRound, Pencil } from 'lucide-react';
import type { EnvironmentModel, EnvironmentType } from '../../api/types';
import { createEnvironment, deleteEnvironment, listEnvironments, updateEnvironment } from '../../api/environments';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { Switch } from '../../components/ui/switch';
import { useConfirm } from '../../components/ui/confirm-dialog';
import { EnvironmentTestDataDialog } from './EnvironmentTestDataDialog';
import { EnvironmentVariablesDialog } from './EnvironmentVariablesDialog';

const environmentTypes: EnvironmentType[] = ['Development', 'Test', 'Staging', 'Production', 'Sandbox'];

export function EnvironmentsTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<EnvironmentModel | null>(null);
  const [testDataFor, setTestDataFor] = useState<EnvironmentModel | null>(null);
  const [variablesFor, setVariablesFor] = useState<EnvironmentModel | null>(null);
  const [form, setForm] = useState<{ name: string; type: EnvironmentType; baseUrl: string; isDefault: boolean }>({
    name: '', type: 'Staging', baseUrl: '', isDefault: false,
  });

  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
  });

  const openCreate = () => {
    setEditing(null);
    setForm({ name: '', type: 'Staging', baseUrl: '', isDefault: false });
    setDialogOpen(true);
  };

  const openEdit = (env: EnvironmentModel) => {
    setEditing(env);
    setForm({ name: env.name, type: env.type, baseUrl: env.baseUrl, isDefault: env.isDefault });
    setDialogOpen(true);
  };

  const saveMutation = useMutation({
    mutationFn: () => (editing
      ? updateEnvironment({ id: editing.id, projectId, ...form })
      : createEnvironment({ projectId, ...form })),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['environments', projectId] });
      queryClient.invalidateQueries({ queryKey: ['project', projectId] });
      setDialogOpen(false);
      setEditing(null);
      setForm({ name: '', type: 'Staging', baseUrl: '', isDefault: false });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteEnvironment(projectId, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['environments', projectId] }),
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Environments</CardTitle>
              <CardDescription>Deployment targets explorations and executions run against.</CardDescription>
            </div>
            <Button variant="outline" size="sm" onClick={openCreate}>
              <Plus className="h-4 w-4 mr-1.5" /> Add environment
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Base URL</TableHead>
                <TableHead>Default</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {environments.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="text-center py-8 text-muted-foreground text-sm">
                    No environments yet. Add one with your application's URL.
                  </TableCell>
                </TableRow>
              )}
              {environments.map((env) => (
                <TableRow key={env.id}>
                  <TableCell className="font-medium">{env.name}</TableCell>
                  <TableCell><Badge variant="outline">{env.type}</Badge></TableCell>
                  <TableCell>
                    <a href={env.baseUrl} target="_blank" rel="noreferrer"
                      className="flex items-center gap-1 text-violet-600 hover:underline text-sm">
                      {env.baseUrl} <ExternalLink className="h-3 w-3" />
                    </a>
                  </TableCell>
                  <TableCell>
                    {env.isDefault ? <Badge variant="default">Default</Badge> : <span className="text-muted-foreground">—</span>}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button variant="outline" size="sm" className="h-8" aria-label={`Environment variables for ${env.name}`}
                        onClick={() => setVariablesFor(env)}>
                        <KeyRound className="h-3.5 w-3.5 mr-1.5" /> Variables
                      </Button>
                      <Button variant="outline" size="sm" className="h-8" aria-label={`Manage test data for ${env.name}`}
                        onClick={() => setTestDataFor(env)}>
                        <Database className="h-3.5 w-3.5 mr-1.5" /> Test data
                      </Button>
                      <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground" aria-label={`Edit environment ${env.name}`}
                        onClick={() => openEdit(env)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" aria-label={`Delete environment ${env.name}`}
                        onClick={async () => {
                          if (await confirm({ title: 'Delete environment?', description: `"${env.name}" will be permanently removed.`, confirmText: 'Delete', tone: 'destructive' })) {
                            deleteMutation.mutate(env.id);
                          }
                        }}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>{editing ? 'Edit environment' : 'Add environment'}</DialogTitle></DialogHeader>
          <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="space-y-4 pt-1">
            {saveMutation.isError && <Alert severity="error">{getErrorMessage(saveMutation.error)}</Alert>}
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input placeholder="Production" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required autoFocus />
            </div>
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select value={form.type} onValueChange={(v) => setForm({ ...form, type: v as EnvironmentType })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {environmentTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Base URL</Label>
              <Input placeholder="https://app.example.com" type="url" value={form.baseUrl}
                onChange={(e) => setForm({ ...form, baseUrl: e.target.value })} required />
            </div>
            <div className="flex items-center gap-3">
              <Switch checked={form.isDefault} onCheckedChange={(v) => setForm({ ...form, isDefault: v })} />
              <Label>Set as default</Label>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button>
              <Button type="submit" disabled={saveMutation.isPending}>
                {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
                {editing ? 'Save changes' : 'Add'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {testDataFor && (
        <EnvironmentTestDataDialog
          projectId={projectId}
          environmentId={testDataFor.id}
          environmentName={testDataFor.name}
          onClose={() => setTestDataFor(null)}
        />
      )}

      {variablesFor && (
        <EnvironmentVariablesDialog
          projectId={projectId}
          environmentId={variablesFor.id}
          environmentName={variablesFor.name}
          onClose={() => setVariablesFor(null)}
        />
      )}
    </div>
  );
}
