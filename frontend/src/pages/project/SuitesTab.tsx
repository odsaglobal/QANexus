import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Play, Trash2, Pencil, Loader2, Layers, ChevronRight } from 'lucide-react';
import type { Scenario, TestSuite } from '../../api/types';
import {
  createTestSuite, deleteTestSuite, listTestSuites, runTestSuite, updateTestSuite,
} from '../../api/suites';
import { listScenarios } from '../../api/scenarios';
import { listEnvironments } from '../../api/environments';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '../../components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { useConfirm } from '../../components/ui/confirm-dialog';
import { cn } from '../../lib/utils';

export function SuitesTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<TestSuite | null>(null);
  const [runFor, setRunFor] = useState<TestSuite | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data: suites = [], isLoading } = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteTestSuite(projectId, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] }),
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Test Suites</CardTitle>
              <CardDescription>
                Group scenarios into suites and run the whole suite sequentially in one browser session on any environment.
              </CardDescription>
            </div>
            <Button size="sm" onClick={() => { setEditing(null); setEditorOpen(true); }}>
              <Plus className="h-4 w-4 mr-1.5" /> New suite
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {error && <Alert severity="error" className="mb-4 text-sm">{error}</Alert>}

          {isLoading ? (
            <div className="py-10 text-center text-muted-foreground text-sm">
              <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2" /> Loading…
            </div>
          ) : suites.length === 0 ? (
            <Alert severity="info" className="text-sm">
              No suites yet. Create a suite to group scenarios and run them together.
            </Alert>
          ) : (
            <div className="space-y-2">
              {suites.map((suite) => (
                <div key={suite.id}
                  className="flex items-center justify-between rounded-lg border border-border p-3 hover:bg-gray-50 transition-colors">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <Layers className="h-4 w-4 text-violet-500 shrink-0" />
                      <span className="font-medium truncate">{suite.name}</span>
                      <Badge variant="secondary">{suite.scenarioCount} scenarios</Badge>
                    </div>
                    {suite.description && (
                      <p className="text-xs text-muted-foreground mt-0.5 ml-6 truncate max-w-lg">{suite.description}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    <Button size="sm" onClick={() => { setError(null); setRunFor(suite); }} disabled={suite.scenarioCount === 0}>
                      <Play className="h-4 w-4 mr-1.5" /> Run
                    </Button>
                    <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground" aria-label={`Edit ${suite.name}`}
                      onClick={() => { setEditing(suite); setEditorOpen(true); }}>
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" aria-label={`Delete ${suite.name}`}
                      onClick={async () => {
                        if (await confirm({ title: 'Delete suite?', description: `"${suite.name}" will be removed. Scenarios themselves are not deleted.`, confirmText: 'Delete', tone: 'destructive' })) {
                          deleteMutation.mutate(suite.id);
                        }
                      }}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {editorOpen && (
        <SuiteEditorDialog
          projectId={projectId}
          suite={editing}
          onClose={() => setEditorOpen(false)}
          onSaved={() => {
            setEditorOpen(false);
            queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
          }}
        />
      )}

      {runFor && (
        <RunSuiteDialog
          projectId={projectId}
          suite={runFor}
          onClose={() => setRunFor(null)}
          onError={setError}
        />
      )}
    </div>
  );
}

function SuiteEditorDialog({
  projectId, suite, onClose, onSaved,
}: {
  projectId: string;
  suite: TestSuite | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(suite?.name ?? '');
  const [description, setDescription] = useState(suite?.description ?? '');
  const [selected, setSelected] = useState<string[]>(suite?.scenarios.map((s) => s.scenarioId) ?? []);
  const [error, setError] = useState<string | null>(null);

  const { data: scenarios = [] } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const toggle = (id: string) =>
    setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = { projectId, name: name.trim(), description: description.trim() || undefined, scenarioIds: selected };
      return suite
        ? updateTestSuite({ id: suite.id, ...payload })
        : createTestSuite(payload);
    },
    onSuccess: onSaved,
    onError: (e) => setError(getErrorMessage(e)),
  });

  const orderIndex = (id: string) => selected.indexOf(id);

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{suite ? 'Edit suite' : 'New suite'}</DialogTitle>
        </DialogHeader>
        <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="space-y-4 pt-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          <div className="space-y-1.5">
            <Label>Name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Checkout regression" required autoFocus />
          </div>
          <div className="space-y-1.5">
            <Label>Description <span className="text-muted-foreground">(optional)</span></Label>
            <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What this suite covers…" />
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label>Scenarios <span className="text-muted-foreground">(run in the order selected)</span></Label>
              <span className="text-xs text-muted-foreground">{selected.length} selected</span>
            </div>
            <div className="max-h-64 overflow-y-auto rounded-lg border border-border divide-y divide-border">
              {scenarios.length === 0 && (
                <p className="p-3 text-sm text-muted-foreground">No scenarios in this project yet.</p>
              )}
              {scenarios.map((s: Scenario) => {
                const idx = orderIndex(s.id);
                const isSelected = idx >= 0;
                return (
                  <button type="button" key={s.id} onClick={() => toggle(s.id)}
                    className={cn('flex w-full items-center gap-2 p-2.5 text-left text-sm hover:bg-gray-50', isSelected && 'bg-violet-50')}>
                    <span className={cn(
                      'flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-[10px] font-semibold',
                      isSelected ? 'border-violet-500 bg-violet-500 text-white' : 'border-gray-300 text-transparent',
                    )}>
                      {isSelected ? idx + 1 : ''}
                    </span>
                    <span className="truncate flex-1">{s.title}</span>
                    <Badge variant="outline" className="shrink-0">{s.steps.length} steps</Badge>
                  </button>
                );
              })}
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={!name.trim() || saveMutation.isPending}>
              {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
              {suite ? 'Save changes' : 'Create suite'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RunSuiteDialog({
  projectId, suite, onClose, onError,
}: {
  projectId: string;
  suite: TestSuite;
  onClose: () => void;
  onError: (msg: string) => void;
}) {
  const [environmentId, setEnvironmentId] = useState<string>('');

  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
  });

  const defaultEnv = useMemo(
    () => environments.find((e) => e.isDefault) ?? environments[0],
    [environments],
  );
  const effectiveEnv = environmentId || defaultEnv?.id || '';

  const runMutation = useMutation({
    mutationFn: () => runTestSuite(projectId, suite.id, effectiveEnv || undefined),
    onSuccess: onClose,
    onError: (e) => { onError(getErrorMessage(e)); onClose(); },
  });

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Run suite: {suite.name}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-1">
          <p className="text-sm text-muted-foreground">
            {suite.scenarioCount} scenarios will run sequentially in one browser session. Follow the live browser in the Page Explorer tab.
          </p>

          {environments.length === 0 ? (
            <Alert severity="warning" className="text-sm">
              Add an environment with a base URL before running a suite.
            </Alert>
          ) : (
            <div className="space-y-1.5">
              <Label>Environment</Label>
              <Select value={effectiveEnv} onValueChange={setEnvironmentId}>
                <SelectTrigger><SelectValue placeholder="Select an environment" /></SelectTrigger>
                <SelectContent>
                  {environments.map((env) => (
                    <SelectItem key={env.id} value={env.id}>
                      {env.name} — {env.baseUrl}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="rounded-lg border border-border divide-y divide-border max-h-48 overflow-y-auto">
            {suite.scenarios.map((s) => (
              <div key={s.scenarioId} className="flex items-center gap-2 p-2 text-sm">
                <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                <span className="truncate">{s.title ?? s.scenarioId}</span>
              </div>
            ))}
          </div>
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="button" disabled={environments.length === 0 || runMutation.isPending}
            onClick={() => runMutation.mutate()}>
            {runMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Play className="h-4 w-4 mr-1.5" />}
            Run suite
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
