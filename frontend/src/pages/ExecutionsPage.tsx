import { useEffect, useMemo, useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Clock3, Play, Search, Loader2, RefreshCw } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '../components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Alert } from '../components/ui/alert';
import { listProjects } from '../api/projects';
import { listExplorationSessions } from '../api/explorer';
import { listScenarios, runScenario } from '../api/scenarios';
import { listTestSuites, runTestSuite } from '../api/suites';import { listEnvironments } from '../api/environments';
import { getStoredProjectId, setStoredProjectId, getSelectedEnvironmentId } from '../lib/projectSelection';
import { getErrorMessage } from '../lib/apiClient';
import type { ExplorationSession, ExplorationStatus, Scenario, EnvironmentModel, TestSuite } from '../api/types';
import { LiveExplorationDialog } from './project/LiveExplorationDialog';

const activeStatuses: ExplorationStatus[] = ['Pending', 'Running'];

function statusVariant(status: ExplorationStatus): 'success' | 'destructive' | 'info' | 'secondary' | 'warning' {
  if (status === 'Completed') return 'success';
  if (status === 'Failed') return 'destructive';
  if (status === 'Cancelled') return 'warning';
  if (status === 'Running') return 'info';
  return 'secondary';
}

function formatDuration(startIso?: string | null, endIso?: string | null): string {
  if (!startIso) return '--';
  const start = new Date(startIso).getTime();
  const end = endIso ? new Date(endIso).getTime() : Date.now();
  const secs = Math.max(0, Math.round((end - start) / 1000));
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  const s = secs % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return h > 0 ? `${pad(h)}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

function shortId(id: string): string {
  return `E-${id.slice(0, 8)}`;
}

export function ExecutionsPage() {
  const [selectedProjectId, setSelectedProjectId] = useState<string>(() => getStoredProjectId());
  const [q, setQ] = useState('');
  const [status, setStatus] = useState<'all' | ExplorationStatus>('all');
  const [runDialogOpen, setRunDialogOpen] = useState(false);
  const [liveSessionId, setLiveSessionId] = useState<string | null>(null);
  const [liveTitle, setLiveTitle] = useState('');

  const projectsQuery = useQuery({
    queryKey: ['projects', { page: 1, pageSize: 100 }],
    queryFn: () => listProjects({ page: 1, pageSize: 100 }),
  });
  const projects = projectsQuery.data?.items ?? [];
  const activeProject = projects.find((p) => p.id === selectedProjectId) ?? projects[0];
  const activeProjectId = activeProject?.id ?? '';

  useEffect(() => {
    if (activeProjectId && selectedProjectId !== activeProjectId) setSelectedProjectId(activeProjectId);
  }, [activeProjectId, selectedProjectId]);
  useEffect(() => {
    if (selectedProjectId) setStoredProjectId(selectedProjectId);
  }, [selectedProjectId]);

  const sessionsQuery = useQuery({
    queryKey: ['exploration-sessions', activeProjectId],
    queryFn: () => listExplorationSessions(activeProjectId),
    enabled: Boolean(activeProjectId),
    refetchInterval: (query) => {
      const data = query.state.data as ExplorationSession[] | undefined;
      const anyActive = data?.some((s) => activeStatuses.includes(s.status));
      return anyActive ? 3000 : false;
    },
  });

  const scenariosQuery = useQuery({
    queryKey: ['scenarios', activeProjectId],
    queryFn: () => listScenarios(activeProjectId),
    enabled: Boolean(activeProjectId),
  });

  const suitesQuery = useQuery({
    queryKey: ['test-suites', activeProjectId],
    queryFn: () => listTestSuites(activeProjectId),
    enabled: Boolean(activeProjectId),
  });

  const sessions = useMemo(() => sessionsQuery.data ?? [], [sessionsQuery.data]);
  const scenarios = scenariosQuery.data ?? [];
  const suites = suitesQuery.data ?? [];
  const scenarioTitle = useMemo(() => {
    const map = new Map(scenarios.map((s) => [s.id, s.title]));
    const suiteMap = new Map(suites.map((s) => [s.id, s.name]));
    return (session: ExplorationSession): string => {
      if (session.suiteId && suiteMap.has(session.suiteId)) return `Suite: ${suiteMap.get(session.suiteId)}`;
      if (session.scenarioId && map.has(session.scenarioId)) return map.get(session.scenarioId)!;
      if (session.recordedScenarioId && map.has(session.recordedScenarioId)) return `Recorded: ${map.get(session.recordedScenarioId)}`;
      if (session.prompt) return session.prompt;
      if (session.suiteId) return 'Suite run';
      if (session.scenarioId) return 'Scenario run';
      return 'Application exploration';
    };
  }, [scenarios, suites]);

  const filtered = useMemo(() => {
    const needle = q.toLowerCase();
    return sessions
      .filter((s) => {
        const label = scenarioTitle(s).toLowerCase();
        const textOk = !needle || label.includes(needle) || shortId(s.id).toLowerCase().includes(needle);
        const statusOk = status === 'all' || s.status === status;
        return textOk && statusOk;
      })
      .sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime());
  }, [sessions, q, status, scenarioTitle]);

  const total = sessions.length;
  const completed = sessions.filter((s) => s.status === 'Completed').length;
  const finished = sessions.filter((s) => s.status === 'Completed' || s.status === 'Failed').length;
  const passRate = finished === 0 ? 0 : Math.round((completed / finished) * 100);
  const runningCount = sessions.filter((s) => activeStatuses.includes(s.status)).length;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Executions</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Real scenario runs — execute explored scenarios and track their results.</p>
        </div>
        <div className="flex items-center gap-2">
          {projects.length > 0 && (
            <Select value={activeProjectId} onValueChange={setSelectedProjectId}>
              <SelectTrigger className="w-[220px]"><SelectValue placeholder="Select a project" /></SelectTrigger>
              <SelectContent>
                {projects.map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
              </SelectContent>
            </Select>
          )}
          <Button variant="outline" size="icon" aria-label="Refresh" onClick={() => sessionsQuery.refetch()}>
            <RefreshCw className={sessionsQuery.isFetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
          </Button>
          <Button onClick={() => setRunDialogOpen(true)} disabled={!activeProjectId}>
            <Play className="h-4 w-4 mr-1.5" /> Run
          </Button>
        </div>
      </div>

      {!activeProjectId ? (
        <Alert severity="info" className="text-sm">Select or create a project to see its executions.</Alert>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Card>
              <CardHeader className="pb-1"><CardTitle className="text-sm">Total runs</CardTitle></CardHeader>
              <CardContent className="text-2xl font-bold">{total}</CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-1"><CardTitle className="text-sm">Pass rate <span className="font-normal text-muted-foreground">(of finished)</span></CardTitle></CardHeader>
              <CardContent className="text-2xl font-bold">{passRate}%</CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-1"><CardTitle className="text-sm">Active queue</CardTitle></CardHeader>
              <CardContent className="text-2xl font-bold flex items-center gap-2"><Clock3 className="h-5 w-5 text-violet-600" /> {runningCount}</CardContent>
            </Card>
          </div>

          <div className="flex gap-3">
            <div className="relative max-w-sm w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input className="pl-9" placeholder="Search by id or scenario..." value={q} onChange={(e) => setQ(e.target.value)} />
            </div>
            <select
              value={status}
              onChange={(e) => setStatus(e.target.value as 'all' | ExplorationStatus)}
              className="h-10 rounded-md border border-input bg-background px-3 text-sm"
            >
              <option value="all">All statuses</option>
              <option value="Completed">Completed</option>
              <option value="Failed">Failed</option>
              <option value="Running">Running</option>
              <option value="Pending">Pending</option>
              <option value="Cancelled">Cancelled</option>
            </select>
          </div>

          <Card>
            <CardContent className="pt-6">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>ID</TableHead>
                    <TableHead>Scenario</TableHead>
                    <TableHead>Started</TableHead>
                    <TableHead>Duration</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sessionsQuery.isLoading && (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-10 text-muted-foreground text-sm">
                        <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading executions…
                      </TableCell>
                    </TableRow>
                  )}
                  {!sessionsQuery.isLoading && filtered.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} className="text-center py-10 text-muted-foreground text-sm">
                        No executions yet. Click “Run scenario” to execute an explored scenario.
                      </TableCell>
                    </TableRow>
                  )}
                  {filtered.map((s) => {
                    const title = scenarioTitle(s);
                    const live = activeStatuses.includes(s.status);
                    return (
                      <TableRow key={s.id}>
                        <TableCell className="font-medium font-mono text-xs">{shortId(s.id)}</TableCell>
                        <TableCell className="max-w-[420px] truncate" title={title}>{title}</TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {s.startedAtUtc ? new Date(s.startedAtUtc).toLocaleString() : new Date(s.createdAtUtc).toLocaleString()}
                        </TableCell>
                        <TableCell className="text-xs">{formatDuration(s.startedAtUtc, s.completedAtUtc)}</TableCell>
                        <TableCell><Badge variant={statusVariant(s.status)}>{s.status}</Badge></TableCell>
                        <TableCell className="text-right">
                          <Button
                            variant="ghost" size="sm" className="h-7"
                            onClick={() => { setLiveTitle(title); setLiveSessionId(s.id); }}
                          >
                            {live ? 'Watch live' : 'View'}
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </>
      )}

      {runDialogOpen && activeProjectId && (
        <RunScenarioDialog
          projectId={activeProjectId}
          onClose={() => setRunDialogOpen(false)}
          onStarted={(session, title) => {
            setRunDialogOpen(false);
            setLiveTitle(title);
            setLiveSessionId(session.id);
            sessionsQuery.refetch();
          }}
        />
      )}

      {liveSessionId && (
        <LiveExplorationDialog
          sessionId={liveSessionId}
          projectId={activeProjectId}
          title={liveTitle}
          mode="run"
          open={!!liveSessionId}
          onClose={() => { setLiveSessionId(null); sessionsQuery.refetch(); }}
        />
      )}
    </div>
  );
}

function RunScenarioDialog({
  projectId, onClose, onStarted,
}: {
  projectId: string;
  onClose: () => void;
  onStarted: (session: ExplorationSession, title: string) => void;
}) {
  const [mode, setMode] = useState<'scenario' | 'suite'>('scenario');
  const [scenarioId, setScenarioId] = useState('');
  const [suiteId, setSuiteId] = useState('');
  const [environmentId, setEnvironmentId] = useState<string>(() => getSelectedEnvironmentId());
  const [error, setError] = useState<string | null>(null);

  const { data: scenarios = [] } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });
  const { data: suites = [] } = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
  });
  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
  });

  const runnable = useMemo(() => scenarios.filter((s: Scenario) => s.steps.length > 0), [scenarios]);
  const runnableSuites = useMemo(() => suites.filter((s: TestSuite) => s.scenarioCount > 0), [suites]);
  const defaultEnv = useMemo(
    () => environments.find((e: EnvironmentModel) => e.isDefault) ?? environments[0],
    [environments],
  );
  const effectiveEnv = environmentId || defaultEnv?.id || '';

  const runMutation = useMutation({
    mutationFn: () => mode === 'suite'
      ? runTestSuite(projectId, suiteId, effectiveEnv || undefined)
      : runScenario(projectId, scenarioId, effectiveEnv || undefined),
    onSuccess: (session) => {
      const title = mode === 'suite'
        ? `Suite: ${runnableSuites.find((s) => s.id === suiteId)?.name ?? 'Test suite'}`
        : (runnable.find((s) => s.id === scenarioId)?.title ?? 'Scenario run');
      onStarted(session, title);
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const canRun = environments.length > 0
    && (mode === 'suite' ? Boolean(suiteId) : Boolean(scenarioId))
    && !runMutation.isPending;

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2"><Play className="h-4 w-4 text-violet-500" /> Run a test</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          {/* Mode toggle */}
          <div className="inline-flex rounded-lg border border-border p-0.5 text-sm">
            <button
              type="button"
              onClick={() => { setMode('scenario'); setError(null); }}
              className={mode === 'scenario' ? 'rounded-md bg-violet-600 px-3 py-1.5 font-medium text-white' : 'rounded-md px-3 py-1.5 text-muted-foreground hover:text-foreground'}
            >
              Single scenario
            </button>
            <button
              type="button"
              onClick={() => { setMode('suite'); setError(null); }}
              className={mode === 'suite' ? 'rounded-md bg-violet-600 px-3 py-1.5 font-medium text-white' : 'rounded-md px-3 py-1.5 text-muted-foreground hover:text-foreground'}
            >
              Whole suite
            </button>
          </div>

          {mode === 'scenario' ? (
            <>
              <p className="text-sm text-muted-foreground">
                Executes the scenario's saved steps as a test. Only scenarios with steps (explored or authored) are listed.
              </p>
              <div className="space-y-1.5">
                <Label>Scenario</Label>
                {runnable.length === 0 ? (
                  <Alert severity="warning" className="text-sm">
                    No runnable scenarios. Explore a scenario first (Scenarios tab) to ground its steps.
                  </Alert>
                ) : (
                  <Select value={scenarioId} onValueChange={setScenarioId}>
                    <SelectTrigger><SelectValue placeholder="Select a scenario" /></SelectTrigger>
                    <SelectContent>
                      {runnable.map((s) => (
                        <SelectItem key={s.id} value={s.id}>{s.title} ({s.steps.length} steps)</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            </>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                Runs every scenario in the suite sequentially in one browser session.
              </p>
              <div className="space-y-1.5">
                <Label>Suite</Label>
                {runnableSuites.length === 0 ? (
                  <Alert severity="warning" className="text-sm">
                    No suites with scenarios. Create a suite in the Scenarios tab first.
                  </Alert>
                ) : (
                  <Select value={suiteId} onValueChange={setSuiteId}>
                    <SelectTrigger><SelectValue placeholder="Select a suite" /></SelectTrigger>
                    <SelectContent>
                      {runnableSuites.map((s) => (
                        <SelectItem key={s.id} value={s.id}>{s.name} ({s.scenarioCount} scenarios)</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            </>
          )}

          <div className="space-y-1.5">
            <Label>Environment</Label>
            {environments.length === 0 ? (
              <Alert severity="warning" className="text-sm">Add an environment with a base URL first.</Alert>
            ) : (
              <Select value={effectiveEnv} onValueChange={setEnvironmentId}>
                <SelectTrigger><SelectValue placeholder="Select an environment" /></SelectTrigger>
                <SelectContent>
                  {environments.map((env: EnvironmentModel) => (
                    <SelectItem key={env.id} value={env.id}>{env.name} — {env.baseUrl}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={!canRun} onClick={() => runMutation.mutate()}>
            {runMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Play className="h-4 w-4 mr-1.5" />}
            Run
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
