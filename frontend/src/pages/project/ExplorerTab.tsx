import { useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Play, Loader2, Compass, Sparkles, ListPlus, CheckCircle2, XCircle, Wrench, MinusCircle,
  Terminal, Layers, FileText, RotateCcw, Search, ChevronsUpDown, Check, ShieldCheck, Square,
} from 'lucide-react';
import type { Scenario, StepRunStatus, TestSuite } from '../../api/types';
import { cancelExploration, getExplorationSession, startExploration } from '../../api/explorer';
import { listScenarios, exploreScenario, applyProposedSteps } from '../../api/scenarios';
import { listRequirements, getRequirement } from '../../api/requirements';
import { listEnvironments } from '../../api/environments';
import { createTestSuite, listTestSuites, updateTestSuite } from '../../api/suites';
import { getSelectedEnvironmentId } from '../../lib/projectSelection';
import { getErrorMessage } from '../../lib/apiClient';
import { useExplorationStream } from '../../lib/useExplorationStream';
import type { ExplorationLiveStep, ExplorationLogLine, ExplorationLiveTab } from '../../lib/useExplorationStream';
import { LiveTabsBar } from './LiveExplorationDialog';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { Textarea } from '../../components/ui/textarea';
import { Label } from '../../components/ui/label';
import { Input } from '../../components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog';
import { cn } from '../../lib/utils';

type Mode = 'scenario' | 'prompt';

const logColor: Record<string, string> = {
  info: 'text-slate-300', success: 'text-green-400', warn: 'text-amber-400', error: 'text-red-400',
  // Dispatched but not yet verified — dimmed until its outcome rewrites the line.
  pending: 'text-slate-400',
};

export function ExplorerTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const [mode, setMode] = useState<Mode>('scenario');
  const [environmentId, setEnvironmentId] = useState('');
  const [scenarioId, setScenarioId] = useState('');
  const [prompt, setPrompt] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sessionId, setSessionId] = useState<string | null>(null);
  // The scenario to surface in results: the selected one (scenario mode) or the recorded one (prompt mode).
  const [resultScenarioId, setResultScenarioId] = useState<string | null>(null);
  const [addToSuiteOpen, setAddToSuiteOpen] = useState(false);

  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
  });

  const { data: scenarios = [] } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const { data: pickerSuites = [] } = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
  });

  // Feature labels (Module › Feature) to group scenarios in the picker.
  const { data: requirements = [] } = useQuery({
    queryKey: ['requirements', projectId],
    queryFn: () => listRequirements(projectId),
  });
  const analyzedIds = requirements.filter((r) => r.status === 'Analyzed').map((r) => r.id);
  const detailQueries = useQuery({
    queryKey: ['requirement-features', projectId, analyzedIds],
    queryFn: async () => Promise.all(analyzedIds.map((id) => getRequirement(projectId, id))),
    enabled: analyzedIds.length > 0,
  });
  const featureLabels = useMemo(() => {
    const map = new Map<string, string>();
    for (const detail of detailQueries.data ?? []) {
      for (const module of detail.modules) {
        for (const feature of module.features) {
          map.set(feature.id, `${module.name} › ${feature.name}`);
        }
      }
    }
    return map;
  }, [detailQueries.data]);

  useEffect(() => {
    if (environments.length > 0 && !environmentId) {
      const selected = getSelectedEnvironmentId();
      const env = environments.find((e) => e.id === selected)
        ?? environments.find((e) => e.isDefault) ?? environments[0];
      setEnvironmentId(env.id);
    }
  }, [environments, environmentId]);

  // Poll the session for status + recordedScenarioId (prompt mode).
  const { data: session } = useQuery({
    queryKey: ['exploration-session', projectId, sessionId],
    queryFn: () => getExplorationSession(projectId, sessionId!),
    enabled: Boolean(sessionId),
    refetchInterval: (query) => {
      const s = query.state.data?.status;
      return s === 'Pending' || s === 'Running' ? 2500 : false;
    },
  });

  const isLive = session?.status === 'Pending' || session?.status === 'Running';
  const stream = useExplorationStream(sessionId, Boolean(sessionId) && isLive);

  // When a run finishes, resolve which scenario holds the results and refresh scenario data.
  useEffect(() => {
    if (!session) return;
    if (session.status === 'Completed' || session.status === 'Failed' || session.status === 'Cancelled') {
      const target = mode === 'prompt' ? session.recordedScenarioId : session.scenarioId;
      if (target) setResultScenarioId(target);
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
    }
  }, [session, mode, projectId, queryClient]);

  const startMutation = useMutation({
    mutationFn: async () => {
      const envId = environmentId || getSelectedEnvironmentId();
      if (!envId) throw new Error('Select an environment first.');
      if (mode === 'scenario') {
        if (!scenarioId) throw new Error('Select a scenario to explore.');
        return exploreScenario(projectId, scenarioId, envId);
      }
      if (!prompt.trim()) throw new Error('Describe the flow you want to explore.');
      return startExploration({ projectId, environmentId: envId, prompt: prompt.trim() });
    },
    onSuccess: (s) => {
      setError(null);
      setResultScenarioId(null);
      setSessionId(s.id);
      queryClient.invalidateQueries({ queryKey: ['exploration-session', projectId, s.id] });
    },
    onError: (e) => setError(e instanceof Error ? e.message : getErrorMessage(e)),
  });

  // Cancelling is a two-stage affair: the API flips the session to Cancelled immediately, but the agent
  // only notices at its next turn boundary, so keep the button in its "stopping" state until the polled
  // session actually reports a terminal status rather than snapping back to "Explore" straight away.
  const stopMutation = useMutation({
    mutationFn: () => cancelExploration(projectId, sessionId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['exploration-session', projectId, sessionId] });
      queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] });
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const resultScenario = useMemo<Scenario | null>(
    () => scenarios.find((s) => s.id === resultScenarioId) ?? null,
    [scenarios, resultScenarioId],
  );

  const reset = () => {
    setSessionId(null);
    setResultScenarioId(null);
    setError(null);
  };

  const running = isLive || startMutation.isPending;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2"><Compass className="h-5 w-5 text-violet-500" /> Scenario Explorer</CardTitle>
          <CardDescription>
            Pick a scenario or describe a flow in plain English. The AI drives a real browser, discovers the
            steps, and you can add the resulting scenario to a suite to run later.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          {/* Mode toggle */}
          <div className="inline-flex rounded-lg border border-border p-0.5 bg-muted/40">
            <button type="button" onClick={() => setMode('scenario')}
              className={cn('flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                mode === 'scenario' ? 'bg-background shadow-sm text-foreground' : 'text-muted-foreground hover:text-foreground')}>
              <FileText className="h-4 w-4" /> Existing scenario
            </button>
            <button type="button" onClick={() => setMode('prompt')}
              className={cn('flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                mode === 'prompt' ? 'bg-background shadow-sm text-foreground' : 'text-muted-foreground hover:text-foreground')}>
              <Sparkles className="h-4 w-4" /> Describe a flow
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-[1fr_260px] gap-4 items-end">
            <div className="space-y-1.5">
              {mode === 'scenario' ? (
                <>
                  <Label>Scenario</Label>
                  <ScenarioPicker
                    scenarios={scenarios}
                    featureLabels={featureLabels}
                    suites={pickerSuites}
                    value={scenarioId}
                    onChange={setScenarioId}
                    disabled={running}
                  />
                </>
              ) : (
                <>
                  <Label>Describe the flow to explore</Label>
                  <Textarea
                    value={prompt}
                    onChange={(e) => setPrompt(e.target.value)}
                    disabled={running}
                    rows={3}
                    placeholder="e.g. Log in with {{username}} / {{password}}, search for 'laptop', add the first result to the cart and go to checkout."
                  />
                </>
              )}
            </div>
            <div className="space-y-1.5">
              <Label>Environment</Label>
              <Select value={environmentId} onValueChange={setEnvironmentId} disabled={running || environments.length === 0}>
                <SelectTrigger><SelectValue placeholder="Select an environment" /></SelectTrigger>
                <SelectContent>
                  {environments.map((e) => <SelectItem key={e.id} value={e.id}>{e.name} — {e.baseUrl}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button
              disabled={running || environments.length === 0 || (mode === 'scenario' ? !scenarioId : !prompt.trim())}
              onClick={() => startMutation.mutate()}
            >
              {running ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Play className="h-4 w-4 mr-1.5" />}
              {running ? 'Exploring…' : 'Explore'}
            </Button>
            {isLive && sessionId && (
              <Button
                variant="destructive"
                disabled={stopMutation.isPending}
                onClick={() => stopMutation.mutate()}
                title="Stop this exploration"
              >
                {stopMutation.isPending
                  ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />
                  : <Square className="h-4 w-4 mr-1.5" />}
                {stopMutation.isPending ? 'Stopping…' : 'Stop'}
              </Button>
            )}
            {sessionId && !running && (
              <Button variant="outline" onClick={reset}>
                <RotateCcw className="h-4 w-4 mr-1.5" /> New exploration
              </Button>
            )}
            {environments.length === 0 && (
              <span className="text-sm text-muted-foreground">Add an environment with a Base URL first.</span>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Live + results */}
      {sessionId && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <LivePanel
            status={session?.status ?? 'Pending'}
            frame={stream.frame}
            connected={stream.connected}
            currentUrl={stream.status?.currentUrl}
            logs={stream.logs}
            steps={stream.steps}
            tabs={stream.tabs}
          />
          <ResultsPanel
            projectId={projectId}
            status={session?.status ?? 'Pending'}
            mode={mode}
            scenario={resultScenario}
            errorMessage={session?.errorMessage}
            onAddToSuite={() => setAddToSuiteOpen(true)}
          />
        </div>
      )}

      {addToSuiteOpen && resultScenario && (
        <AddToSuiteDialog
          projectId={projectId}
          scenario={resultScenario}
          onClose={() => setAddToSuiteOpen(false)}
        />
      )}
    </div>
  );
}

function LivePanel({
  status, frame, connected, currentUrl, logs, steps, tabs,
}: {
  status: string;
  frame: string | null;
  connected: boolean;
  currentUrl?: string | null;
  logs: ExplorationLogLine[];
  steps: ExplorationLiveStep[];
  tabs: ExplorationLiveTab[];
}) {
  const isLive = status === 'Pending' || status === 'Running';
  return (
    <Card className="overflow-hidden">
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-semibold flex items-center gap-2">
            <span className={cn('h-2 w-2 rounded-full', connected && isLive ? 'bg-green-500 animate-pulse' : 'bg-gray-300')} />
            Live browser
          </CardTitle>
          <span className="text-xs text-muted-foreground truncate max-w-[260px]">{currentUrl ?? ''}</span>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        <LiveTabsBar tabs={tabs} />
        <div className="relative bg-slate-900 aspect-video flex items-center justify-center">
          {frame ? (
            <img src={`data:image/jpeg;base64,${frame}`} alt="Live exploration" className="w-full h-full object-contain" />
          ) : (
            <div className="flex flex-col items-center gap-2 text-slate-400">
              {isLive ? <Loader2 className="h-6 w-6 animate-spin" /> : <Terminal className="h-6 w-6" />}
              <p className="text-xs">{isLive ? (connected ? 'Streaming will begin shortly…' : 'Connecting…') : 'No live frame'}</p>
            </div>
          )}
        </div>
        {/* Activity log */}
        <div className="max-h-40 overflow-y-auto bg-slate-950 px-3 py-2 font-mono text-[11px] leading-relaxed">
          {logs.length === 0 ? (
            <p className="text-slate-500">Waiting for activity…</p>
          ) : logs.map((l, i) => (
            <div key={i} className={logColor[l.level] ?? 'text-slate-300'}>{l.message}</div>
          ))}
        </div>
        {steps.length > 0 && (
          <div className="border-t border-border px-3 py-2 space-y-1 max-h-40 overflow-y-auto">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Steps executed</p>
            {steps.map((s, i) => (
              <div key={i} className="flex items-start gap-2 text-xs">
                <StepStatusIcon status={s.status as StepRunStatus | 'Running'} />
                <span className="flex-1"><span className="text-muted-foreground mr-1">{s.stepOrder}.</span>{s.action}</span>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function ResultsPanel({
  projectId, status, mode, scenario, errorMessage, onAddToSuite,
}: {
  projectId: string;
  status: string;
  mode: Mode;
  scenario: Scenario | null;
  errorMessage?: string | null;
  onAddToSuite: () => void;
}) {
  const queryClient = useQueryClient();
  const [applyError, setApplyError] = useState<string | null>(null);
  const done = status === 'Completed' || status === 'Failed' || status === 'Cancelled';

  // Scenario mode surfaces AI-proposed steps; prompt mode records real steps directly.
  const proposed = scenario?.proposedSteps ?? [];
  const steps = mode === 'scenario' && proposed.length > 0 ? proposed : (scenario?.steps ?? []);
  const usingProposed = mode === 'scenario' && proposed.length > 0;

  const applyMutation = useMutation({
    mutationFn: () => applyProposedSteps(projectId, scenario!.id),
    onSuccess: () => {
      setApplyError(null);
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
    },
    onError: (e) => setApplyError(getErrorMessage(e)),
  });

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-semibold">Discovered steps</CardTitle>
          <Badge variant={status === 'Completed' ? 'success' : status === 'Failed' ? 'destructive' : status === 'Cancelled' ? 'secondary' : 'warning'}>
            {status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {applyError && <Alert severity="error" className="text-xs">{applyError}</Alert>}
        {errorMessage && <Alert severity="error" className="text-xs">{errorMessage}</Alert>}

        {!done ? (
          <p className="text-sm text-muted-foreground flex items-center gap-2">
            <Loader2 className="h-4 w-4 animate-spin" /> Exploring the app to discover steps…
          </p>
        ) : !scenario ? (
          <p className="text-sm text-muted-foreground">
            {status === 'Completed'
              ? 'No steps were recorded from this exploration.'
              : 'The exploration did not complete — nothing to add.'}
          </p>
        ) : (
          <>
            <div>
              <p className="text-sm font-medium text-foreground">{scenario.title}</p>
              {usingProposed && (
                <p className="text-xs text-violet-600 mt-0.5">AI-proposed steps from the real app — apply them to save onto the scenario.</p>
              )}
            </div>
            <ol className="space-y-1">
              {steps.map((step, idx) => (
                <li key={idx} className="text-sm flex items-start gap-2">
                  <span className="text-muted-foreground">{idx + 1}.</span>
                  <span className="flex-1">
                    {step.action}
                    {step.expectedResult && (
                      <span className="mt-1 flex items-start gap-1.5 rounded-md border border-sky-200 bg-sky-50 px-2 py-1">
                        <ShieldCheck className="h-3.5 w-3.5 shrink-0 text-sky-600 mt-px" />
                        <span className="text-xs text-sky-900">
                          <span className="font-semibold uppercase tracking-wide text-[10px] text-sky-700 mr-1.5">Validate</span>
                          {step.expectedResult}
                        </span>
                      </span>
                    )}
                  </span>
                </li>
              ))}
              {steps.length === 0 && <li className="text-sm text-muted-foreground">No steps recorded.</li>}
            </ol>

            <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-border">
              {usingProposed && (
                <Button size="sm" variant="outline" disabled={applyMutation.isPending} onClick={() => applyMutation.mutate()}>
                  {applyMutation.isPending ? <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" /> : <CheckCircle2 className="h-3.5 w-3.5 mr-1.5" />}
                  Apply steps to scenario
                </Button>
              )}
              <Button size="sm" onClick={onAddToSuite} disabled={scenario.steps.length === 0 && !usingProposed}>
                <ListPlus className="h-3.5 w-3.5 mr-1.5" /> Add to suite
              </Button>
            </div>
            {usingProposed && (
              <p className="text-xs text-muted-foreground">Tip: apply the steps first so the suite run executes them.</p>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function AddToSuiteDialog({
  projectId, scenario, onClose,
}: {
  projectId: string;
  scenario: Scenario;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [choice, setChoice] = useState<'existing' | 'new'>('existing');
  const [suiteId, setSuiteId] = useState('');
  const [newName, setNewName] = useState('');

  const { data: suites = [] } = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
  });

  useEffect(() => {
    if (suites.length === 0) setChoice('new');
    else if (!suiteId) setSuiteId(suites[0].id);
  }, [suites, suiteId]);

  const saveMutation = useMutation({
    mutationFn: () => {
      if (choice === 'new') {
        if (!newName.trim()) throw new Error('Enter a name for the new suite.');
        return createTestSuite({ projectId, name: newName.trim(), scenarioIds: [scenario.id] });
      }
      const suite = suites.find((s) => s.id === suiteId) as TestSuite | undefined;
      if (!suite) throw new Error('Pick a suite.');
      const ids = suite.scenarios.map((s) => s.scenarioId);
      if (!ids.includes(scenario.id)) ids.push(scenario.id);
      return updateTestSuite({
        id: suite.id, projectId, name: suite.name,
        description: suite.description ?? undefined, scenarioIds: ids,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
      onClose();
    },
    onError: (e) => setError(e instanceof Error ? e.message : getErrorMessage(e)),
  });

  const alreadyIn = (s: TestSuite) => s.scenarios.some((x) => x.scenarioId === scenario.id);

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2"><Layers className="h-4 w-4 text-violet-500" /> Add to suite</DialogTitle>
        </DialogHeader>
        <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="space-y-4 pt-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}
          <p className="text-sm text-muted-foreground">
            Add <span className="font-medium text-foreground">{scenario.title}</span> to a suite so you can run it (and others) together later.
          </p>

          <div className="inline-flex rounded-lg border border-border p-0.5 bg-muted/40">
            <button type="button" onClick={() => setChoice('existing')} disabled={suites.length === 0}
              className={cn('rounded-md px-3 py-1.5 text-sm font-medium transition-colors disabled:opacity-40',
                choice === 'existing' ? 'bg-background shadow-sm' : 'text-muted-foreground')}>
              Existing suite
            </button>
            <button type="button" onClick={() => setChoice('new')}
              className={cn('rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                choice === 'new' ? 'bg-background shadow-sm' : 'text-muted-foreground')}>
              New suite
            </button>
          </div>

          {choice === 'existing' ? (
            <div className="space-y-1.5">
              <Label>Suite</Label>
              <Select value={suiteId} onValueChange={setSuiteId}>
                <SelectTrigger><SelectValue placeholder="Select a suite" /></SelectTrigger>
                <SelectContent>
                  {suites.map((s) => (
                    <SelectItem key={s.id} value={s.id} disabled={alreadyIn(s)}>
                      {s.name} ({s.scenarioCount}){alreadyIn(s) ? ' — already added' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : (
            <div className="space-y-1.5">
              <Label>New suite name</Label>
              <Input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="Checkout regression" autoFocus />
            </div>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={saveMutation.isPending}>
              {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
              Add to suite
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

const typeColor: Record<string, string> = {
  Positive: '#22c55e', Negative: '#ef4444', Boundary: '#f59e0b', Smoke: '#a78bfa',
  Security: '#ec4899', Regression: '#14b8a6', Accessibility: '#8b5cf6', Api: '#0ea5e9',
};

const MAX_RENDER = 300;

const sourceMeta: Record<string, { label: string; variant: 'default' | 'secondary' | 'outline' }> = {
  AiGenerated: { label: 'AI', variant: 'default' },
  Manual: { label: 'Manual', variant: 'secondary' },
  TestRail: { label: 'TestRail', variant: 'outline' },
};

/** Command-palette style scenario picker: search + suite grouping/filter + keyboard nav; fast with 1000s. */
function ScenarioPicker({
  scenarios, featureLabels, suites, value, onChange, disabled,
}: {
  scenarios: Scenario[];
  featureLabels: Map<string, string>;
  suites: TestSuite[];
  value: string;
  onChange: (id: string) => void;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [suiteFilter, setSuiteFilter] = useState<string>('all');
  const [activeIndex, setActiveIndex] = useState(0);
  const listRef = useRef<HTMLDivElement>(null);

  const selected = scenarios.find((s) => s.id === value) ?? null;

  // scenarioId -> the suites that contain it.
  const suiteOf = useMemo(() => {
    const map = new Map<string, { id: string; name: string }[]>();
    for (const suite of suites) {
      for (const ts of suite.scenarios) {
        const arr = map.get(ts.scenarioId) ?? [];
        arr.push({ id: suite.id, name: suite.name });
        map.set(ts.scenarioId, arr);
      }
    }
    return map;
  }, [suites]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const terms = q ? q.split(/\s+/) : [];
    return scenarios.filter((s) => {
      if (suiteFilter !== 'all' && !(suiteOf.get(s.id) ?? []).some((x) => x.id === suiteFilter)) {
        return false;
      }
      if (terms.length === 0) return true;
      const feature = featureLabels.get(s.featureId) ?? '';
      const suiteNames = (suiteOf.get(s.id) ?? []).map((x) => x.name).join(' ');
      const hay = `${s.title} ${s.type} ${s.priority} ${s.source} ${feature} ${suiteNames} ${s.tags.join(' ')}`.toLowerCase();
      return terms.every((t) => hay.includes(t));
    });
  }, [scenarios, query, suiteFilter, featureLabels, suiteOf]);

  const shown = filtered.slice(0, MAX_RENDER);

  // Don't dump every scenario by default — only list once the user narrows by suite or search.
  const shouldList = suiteFilter !== 'all' || query.trim().length > 0;

  // Group by suite; preserve suite order, with a trailing "Not in a suite" group. Flat list drives keyboard nav.
  const { groups, flat } = useMemo(() => {
    if (!shouldList) return { groups: [] as [string, Scenario[]][], flat: [] as Scenario[] };
    let g: [string, Scenario[]][];
    if (suiteFilter !== 'all') {
      const suite = suites.find((s) => s.id === suiteFilter);
      g = [[suite?.name ?? 'Suite', shown]];
    } else {
      g = [];
      for (const suite of suites) {
        const items = shown.filter((s) => (suiteOf.get(s.id) ?? []).some((x) => x.id === suite.id));
        if (items.length) g.push([suite.name, items]);
      }
      const ungrouped = shown.filter((s) => !(suiteOf.get(s.id)?.length));
      if (ungrouped.length) g.push([suites.length ? 'Not in a suite' : 'All scenarios', ungrouped]);
    }
    return { groups: g, flat: g.flatMap(([, items]) => items) };
  }, [shown, suites, suiteFilter, suiteOf, shouldList]);

  useEffect(() => { setActiveIndex(0); }, [query, suiteFilter, open]);

  useEffect(() => {
    if (!open) return;
    const el = listRef.current?.querySelector(`[data-idx="${activeIndex}"]`);
    el?.scrollIntoView({ block: 'nearest' });
  }, [activeIndex, open]);

  const commit = (s?: Scenario) => {
    if (!s) return;
    onChange(s.id);
    setOpen(false);
    setQuery('');
  };

  const onKeyDown = (e: ReactKeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActiveIndex((i) => Math.min(i + 1, flat.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveIndex((i) => Math.max(i - 1, 0)); }
    else if (e.key === 'Enter') { e.preventDefault(); commit(flat[activeIndex]); }
  };

  let flatIdx = -1;

  return (
    <>
      <Button
        type="button"
        variant="outline"
        role="combobox"
        disabled={disabled || scenarios.length === 0}
        onClick={() => setOpen(true)}
        className="w-full justify-between font-normal h-10"
      >
        {selected ? (
          <span className="flex items-center gap-2 min-w-0">
            <span className="h-2 w-2 shrink-0 rounded-full" style={{ background: typeColor[selected.type] ?? '#94a3b8' }} />
            <span className="truncate">{selected.title}</span>
          </span>
        ) : (
          <span className="text-muted-foreground">{scenarios.length ? 'Search & select a scenario…' : 'No scenarios yet'}</span>
        )}
        <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
      </Button>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) setQuery(''); }}>
        <DialogContent className="max-w-xl gap-0 p-0 overflow-hidden">
          <DialogHeader className="sr-only"><DialogTitle>Select a scenario</DialogTitle></DialogHeader>
          <div className="relative border-b border-border">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              autoFocus
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={onKeyDown}
              placeholder="Search by title, suite, type, priority, source or tag…"
              className="h-14 border-0 pl-11 text-base focus-visible:ring-0 focus-visible:ring-offset-0"
            />
          </div>

          {suites.length > 0 && (
            <div className="flex flex-wrap items-center gap-1.5 border-b border-border px-3 py-2">
              <span className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground mr-1">Suite</span>
              <button
                type="button"
                onClick={() => setSuiteFilter('all')}
                className={cn('rounded-full px-2.5 py-1 text-xs font-medium transition-colors',
                  suiteFilter === 'all' ? 'bg-violet-600 text-white' : 'bg-muted text-muted-foreground hover:text-foreground')}
              >
                All ({scenarios.length})
              </button>
              {suites.map((suite) => (
                <button
                  type="button"
                  key={suite.id}
                  onClick={() => setSuiteFilter(suite.id)}
                  className={cn('rounded-full px-2.5 py-1 text-xs font-medium transition-colors',
                    suiteFilter === suite.id ? 'bg-violet-600 text-white' : 'bg-muted text-muted-foreground hover:text-foreground')}
                >
                  {suite.name} ({suite.scenarioCount})
                </button>
              ))}
            </div>
          )}

          <div ref={listRef} className="max-h-[62vh] overflow-y-auto py-1.5">
            {!shouldList ? (
              <div className="px-4 py-12 text-center">
                <Layers className="h-6 w-6 mx-auto mb-2 text-muted-foreground/50" />
                <p className="text-sm text-muted-foreground">
                  {suites.length > 0 ? 'Pick a suite above' : 'Start typing'} or search to find a scenario.
                </p>
                <p className="text-xs text-muted-foreground/70 mt-1">{scenarios.length} scenario{scenarios.length === 1 ? '' : 's'} in this project.</p>
              </div>
            ) : flat.length === 0 ? (
              <div className="px-4 py-12 text-center">
                <Search className="h-6 w-6 mx-auto mb-2 text-muted-foreground/50" />
                <p className="text-sm text-muted-foreground">No scenarios match &ldquo;{query}&rdquo;.</p>
              </div>
            ) : groups.map(([label, items]) => (
              <div key={label} className="mb-1">
                <div className="sticky top-0 z-10 bg-background/95 backdrop-blur px-4 py-1.5 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground flex items-center justify-between">
                  <span className="truncate">{label}</span>
                  <span className="ml-2 shrink-0 text-muted-foreground/70">{items.length}</span>
                </div>
                {items.map((s) => {
                  flatIdx += 1;
                  const idx = flatIdx;
                  const active = idx === activeIndex;
                  const isSelected = s.id === value;
                  const src = sourceMeta[s.source] ?? { label: s.source, variant: 'outline' as const };
                  return (
                    <button
                      type="button"
                      key={`${label}-${s.id}`}
                      data-idx={idx}
                      onMouseEnter={() => setActiveIndex(idx)}
                      onClick={() => commit(s)}
                      className={cn('flex w-full items-center gap-3 px-4 py-2 text-left transition-colors',
                        active ? 'bg-violet-50' : 'hover:bg-muted/60')}
                    >
                      <span className="h-2 w-2 shrink-0 rounded-full" style={{ background: typeColor[s.type] ?? '#94a3b8' }} />
                      <div className="min-w-0 flex-1">
                        <div className={cn('truncate text-sm', isSelected && 'font-medium text-violet-700')}>{s.title}</div>
                        <div className="flex items-center gap-1.5 mt-0.5">
                          <span className="text-[11px] text-muted-foreground">{s.type} · {s.priority} risk {s.risk}</span>
                        </div>
                      </div>
                      <Badge variant={src.variant} className="shrink-0 text-[10px]">{src.label}</Badge>
                      {isSelected && <Check className="h-4 w-4 shrink-0 text-violet-600" />}
                    </button>
                  );
                })}
              </div>
            ))}
          </div>

          <div className="border-t border-border px-4 py-2 flex items-center justify-between text-xs text-muted-foreground">
            <span>
              {filtered.length > MAX_RENDER
                ? `First ${MAX_RENDER} of ${filtered.length} — refine your search`
                : `${filtered.length} of ${scenarios.length} scenario${scenarios.length === 1 ? '' : 's'}`}
            </span>
            <span className="hidden sm:flex items-center gap-2">
              <kbd className="rounded border border-border bg-muted px-1.5 py-0.5">↑↓</kbd> navigate
              <kbd className="rounded border border-border bg-muted px-1.5 py-0.5">↵</kbd> select
            </span>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}

function StepStatusIcon({ status }: { status?: StepRunStatus | 'Running' }) {
  switch (status) {
    case 'Passed': return <CheckCircle2 className="h-4 w-4 text-green-600" />;
    case 'Healed': return <Wrench className="h-4 w-4 text-amber-600" />;
    case 'Failed': return <XCircle className="h-4 w-4 text-red-600" />;
    case 'Skipped': return <MinusCircle className="h-4 w-4 text-gray-400" />;
    case 'Running': return <Loader2 className="h-4 w-4 animate-spin text-violet-600" />;
    default: return <span className="inline-block h-4 w-4 rounded-full border border-gray-300" />;
  }
}
