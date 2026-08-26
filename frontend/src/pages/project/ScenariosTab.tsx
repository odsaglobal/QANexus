import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Sparkles, Edit2, Loader2, FileUp, Plus, Compass, Search,
  CheckCircle2, XCircle, Wrench, MinusCircle, Play, FileText, Layers, ListChecks,
  Trash2, Pencil, ChevronRight, RefreshCw,
} from 'lucide-react';
import type { Scenario, TestSuite, StepRunStatus } from '../../api/types';
import { generateScenariosFromStory, listScenarios, exploreScenario, runScenario, getLatestScenarioRun } from '../../api/scenarios';
import {
  createTestSuite, deleteTestSuite, listTestSuites, runTestSuite, updateTestSuite,
} from '../../api/suites';
import { listEnvironments } from '../../api/environments';
import { getSelectedEnvironmentId } from '../../lib/projectSelection';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Textarea } from '../../components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '../../components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { useConfirm } from '../../components/ui/confirm-dialog';
import { cn } from '../../lib/utils';
import { ScenarioEditorDialog } from './ScenarioEditorDialog';
import { ImportScenariosDialog } from './ImportScenariosDialog';
import { ManualScenarioDialog } from './ManualScenarioDialog';
import { TestRailSyncDialog } from './TestRailSyncDialog';
import { LiveExplorationDialog } from './LiveExplorationDialog';

const typeColor: Record<string, string> = {
  Positive: '#22c55e', Negative: '#ef4444', Boundary: '#f59e0b', Smoke: '#a78bfa',
  Security: '#ec4899', Regression: '#14b8a6', Accessibility: '#8b5cf6', Api: '#0ea5e9',
};

const sourceMeta: Record<string, { label: string; variant: 'default' | 'secondary' | 'outline' }> = {
  AiGenerated: { label: 'AI', variant: 'default' },
  Manual: { label: 'Manual', variant: 'secondary' },
  TestRail: { label: 'TestRail', variant: 'outline' },
};

const ALL_SCENARIOS = '__all__';

export function ScenariosTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selectedSuiteId, setSelectedSuiteId] = useState<string>(ALL_SCENARIOS);
  const [selectedScenarioId, setSelectedScenarioId] = useState<string | null>(null);
  const [storyOpen, setStoryOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [manualScenarioOpen, setManualScenarioOpen] = useState(false);
  const [testRailOpen, setTestRailOpen] = useState(false);
  const [suiteEditorOpen, setSuiteEditorOpen] = useState(false);
  const [editingSuite, setEditingSuite] = useState<TestSuite | null>(null);
  const [runSuiteFor, setRunSuiteFor] = useState<TestSuite | null>(null);

  const { data: scenarios = [], isLoading: scenariosLoading } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const { data: suites = [], isLoading: suitesLoading } = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
  });

  const scenarioById = useMemo(
    () => new Map(scenarios.map((s) => [s.id, s])),
    [scenarios],
  );

  const listScenariosForColumn: Scenario[] = useMemo(() => {
    if (selectedSuiteId === ALL_SCENARIOS) return scenarios;
    const suite = suites.find((s) => s.id === selectedSuiteId);
    if (!suite) return [];
    return [...suite.scenarios]
      .sort((a, b) => a.order - b.order)
      .map((ts) => scenarioById.get(ts.scenarioId))
      .filter((s): s is Scenario => Boolean(s));
  }, [selectedSuiteId, suites, scenarios, scenarioById]);

  // Keep the selected scenario valid for the visible list.
  useEffect(() => {
    if (selectedScenarioId && !listScenariosForColumn.some((s) => s.id === selectedScenarioId)) {
      setSelectedScenarioId(listScenariosForColumn[0]?.id ?? null);
    } else if (!selectedScenarioId && listScenariosForColumn.length > 0) {
      setSelectedScenarioId(listScenariosForColumn[0].id);
    }
  }, [listScenariosForColumn, selectedScenarioId]);

  const selectedScenario = selectedScenarioId ? scenarioById.get(selectedScenarioId) ?? null : null;

  const deleteSuiteMutation = useMutation({
    mutationFn: (id: string) => deleteTestSuite(projectId, id),
    onSuccess: (_r, id) => {
      if (selectedSuiteId === id) setSelectedSuiteId(ALL_SCENARIOS);
      queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle>Scenario Management</CardTitle>
          <CardDescription>
            Generate manual test cases from a story (grounded in your Business Context docs), add them by hand, or import CSV/XLSX — all editable with ordered steps.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}
          {notice && <Alert severity="success" className="text-sm">{notice}</Alert>}
          <div className="flex flex-wrap items-center gap-3">
            <Button onClick={() => { setError(null); setNotice(null); setStoryOpen(true); }}>
              <Sparkles className="h-4 w-4 mr-1.5" /> Generate test cases
            </Button>
            <Button variant="outline" onClick={() => { setError(null); setNotice(null); setManualScenarioOpen(true); }}>
              <Plus className="h-4 w-4 mr-1.5" /> Add scenario
            </Button>
            <Button variant="outline" onClick={() => { setError(null); setNotice(null); setImportOpen(true); }}>
              <FileUp className="h-4 w-4 mr-1.5" /> Import CSV/XLSX
            </Button>
            <Button variant="outline" onClick={() => { setError(null); setNotice(null); setTestRailOpen(true); }}>
              <RefreshCw className="h-4 w-4 mr-1.5" /> Sync from TestRail
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* TestRail-style three-pane: Suites · Scenarios · Steps */}
      <div className="flex rounded-lg border border-border overflow-hidden h-[calc(100vh-20rem)] min-h-[480px]">
        {/* Column 1 — Suites */}
        <div className="w-[240px] shrink-0 border-r border-border flex flex-col min-h-0">
          <div className="px-3 py-2 border-b border-border bg-muted/40 flex items-center gap-2">
            <Layers className="h-4 w-4 text-violet-500" />
            <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Suites</span>
            <Button size="icon" variant="ghost" className="ml-auto h-7 w-7" aria-label="New suite"
              onClick={() => { setEditingSuite(null); setSuiteEditorOpen(true); }}>
              <Plus className="h-4 w-4" />
            </Button>
          </div>
          <div className="flex-1 overflow-y-auto p-1.5 space-y-0.5">
            <button
              type="button"
              onClick={() => setSelectedSuiteId(ALL_SCENARIOS)}
              className={cn(
                'flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm transition-colors',
                selectedSuiteId === ALL_SCENARIOS ? 'bg-violet-50 text-violet-700' : 'hover:bg-muted',
              )}
            >
              <ListChecks className="h-4 w-4 shrink-0 text-muted-foreground" />
              <span className="flex-1 truncate font-medium">All scenarios</span>
              <Badge variant="secondary">{scenarios.length}</Badge>
            </button>

            {(suitesLoading || scenariosLoading) && (
              <div className="py-6 text-center text-muted-foreground text-xs">
                <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading…
              </div>
            )}

            {!suitesLoading && suites.length === 0 && (
              <p className="px-2.5 py-3 text-xs text-muted-foreground">
                No suites yet. Use + to group scenarios into a suite.
              </p>
            )}

            {suites.map((suite) => {
              const active = selectedSuiteId === suite.id;
              return (
                <div
                  key={suite.id}
                  className={cn(
                    'group flex items-center gap-1 rounded-md pl-2.5 pr-1 transition-colors',
                    active ? 'bg-violet-50' : 'hover:bg-muted',
                  )}
                >
                  <button
                    type="button"
                    onClick={() => setSelectedSuiteId(suite.id)}
                    className="flex flex-1 items-center gap-2 py-2 text-left text-sm min-w-0"
                  >
                    <Layers className={cn('h-4 w-4 shrink-0', active ? 'text-violet-500' : 'text-muted-foreground')} />
                    <span className={cn('flex-1 truncate', active && 'text-violet-700 font-medium')}>{suite.name}</span>
                    <Badge variant="secondary">{suite.scenarioCount}</Badge>
                  </button>
                  <div className="flex items-center opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-opacity">
                    <Button
                      size="icon" className="h-7 w-7" variant="ghost"
                      title="Run suite" aria-label={`Run ${suite.name}`}
                      disabled={suite.scenarioCount === 0}
                      onClick={(e) => { e.stopPropagation(); setError(null); setRunSuiteFor(suite); }}
                    >
                      <Play className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      size="icon" className="h-7 w-7 text-muted-foreground" variant="ghost"
                      aria-label={`Edit ${suite.name}`}
                      onClick={(e) => { e.stopPropagation(); setEditingSuite(suite); setSuiteEditorOpen(true); }}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      size="icon" className="h-7 w-7 text-muted-foreground hover:text-destructive" variant="ghost"
                      aria-label={`Delete ${suite.name}`}
                      onClick={async (e) => {
                        e.stopPropagation();
                        if (await confirm({ title: 'Delete suite?', description: `"${suite.name}" will be removed. Scenarios themselves are not deleted.`, confirmText: 'Delete', tone: 'destructive' })) {
                          deleteSuiteMutation.mutate(suite.id);
                        }
                      }}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Column 2 — Scenarios */}
        <div className="w-[300px] shrink-0 border-r border-border flex flex-col min-h-0">
          <div className="px-3 py-2 border-b border-border bg-muted/40 flex items-center gap-2">
            <FileText className="h-4 w-4 text-violet-500" />
            <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Scenarios</span>
            <Badge variant="secondary" className="ml-auto">{listScenariosForColumn.length}</Badge>
          </div>
          <div className="flex-1 overflow-y-auto p-1.5 space-y-0.5">
            {scenariosLoading && (
              <div className="py-6 text-center text-muted-foreground text-xs">
                <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading scenarios…
              </div>
            )}
            {!scenariosLoading && listScenariosForColumn.length === 0 && (
              <p className="px-2.5 py-3 text-xs text-muted-foreground">
                {selectedSuiteId === ALL_SCENARIOS
                  ? 'No scenarios yet. Generate with AI, import a CSV/XLSX, or sync from TestRail.'
                  : 'This suite has no scenarios. Edit the suite to add some.'}
              </p>
            )}
            {listScenariosForColumn.map((scenario) => {
              const active = selectedScenarioId === scenario.id;
              const source = sourceMeta[scenario.source] ?? { label: scenario.source, variant: 'outline' as const };
              return (
                <button
                  type="button"
                  key={scenario.id}
                  onClick={() => setSelectedScenarioId(scenario.id)}
                  className={cn(
                    'flex w-full flex-col gap-1 rounded-md px-2.5 py-2 text-left transition-colors',
                    active ? 'bg-violet-50' : 'hover:bg-muted',
                  )}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full shrink-0"
                      style={{ background: typeColor[scenario.type] ?? '#94a3b8' }} />
                    <span className={cn('flex-1 truncate text-sm', active && 'text-violet-700 font-medium')}>{scenario.title}</span>
                    <Badge variant="outline" className="shrink-0 text-[10px]">{scenario.steps.length}</Badge>
                  </div>
                  <div className="flex items-center gap-1.5 pl-4">
                    <Badge variant={source.variant} className="text-[10px]">{source.label}</Badge>
                    <span className="text-[10px] text-muted-foreground">{scenario.type} · {scenario.priority}</span>
                  </div>
                </button>
              );
            })}
          </div>
        </div>

        {/* Column 3 — Selected scenario steps */}
        <div className="flex-1 min-w-0 flex flex-col min-h-0">
          {selectedScenario ? (
            <ScenarioDetailPanel key={selectedScenario.id} projectId={projectId} scenario={selectedScenario} />
          ) : (
            <div className="flex flex-1 items-center justify-center p-8 text-center text-sm text-muted-foreground">
              Select a scenario to view its steps.
            </div>
          )}
        </div>
      </div>

      {storyOpen && (
        <GenerateFromStoryDialog
          projectId={projectId}
          onClose={() => setStoryOpen(false)}
          onGenerated={(count) => { setStoryOpen(false); setNotice(`Generated ${count} test case${count === 1 ? '' : 's'}.`); }}
        />
      )}
      {importOpen && (
        <ImportScenariosDialog
          projectId={projectId}
          featureId="00000000-0000-0000-0000-000000000000"
          featureLabel="Business context"
          open={importOpen}
          onClose={() => setImportOpen(false)}
          onImported={(count) => setNotice(`Imported ${count} test case${count === 1 ? '' : 's'}.`)}
        />
      )}
      {manualScenarioOpen && (
        <ManualScenarioDialog
          projectId={projectId}
          featureId="00000000-0000-0000-0000-000000000000"
          featureLabel="Business context"
          open={manualScenarioOpen}
          onClose={() => setManualScenarioOpen(false)}
          onCreated={() => setNotice('Test case added.')}
        />
      )}
      {testRailOpen && (
        <TestRailSyncDialog
          projectId={projectId}
          open={testRailOpen}
          onClose={() => setTestRailOpen(false)}
          onSynced={(count) => setNotice(`Synced ${count} test case${count === 1 ? '' : 's'} from TestRail.`)}
        />
      )}
      {suiteEditorOpen && (
        <SuiteEditorDialog
          projectId={projectId}
          suite={editingSuite}
          onClose={() => setSuiteEditorOpen(false)}
          onSaved={(saved) => {
            setSuiteEditorOpen(false);
            if (saved?.id) setSelectedSuiteId(saved.id);
            queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
          }}
        />
      )}
      {runSuiteFor && (
        <RunSuiteDialog
          projectId={projectId}
          suite={runSuiteFor}
          onClose={() => setRunSuiteFor(null)}
          onError={setError}
        />
      )}
    </div>
  );
}

function GenerateFromStoryDialog({
  projectId, onClose, onGenerated,
}: {
  projectId: string;
  onClose: () => void;
  onGenerated: (count: number) => void;
}) {
  const queryClient = useQueryClient();
  const [story, setStory] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => generateScenariosFromStory(projectId, story.trim()),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      onGenerated(created.length);
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2"><Sparkles className="h-4 w-4 text-violet-500" /> Generate test cases</DialogTitle>
        </DialogHeader>
        <form onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }} className="space-y-4 pt-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}
          <p className="text-sm text-muted-foreground">
            Describe a user story or requirement. The AI writes editable manual test cases, grounded in your
            uploaded <span className="font-medium text-foreground">Business Context</span> documents.
          </p>
          <div className="space-y-1.5">
            <Label>Story / requirement</Label>
            <Textarea
              value={story}
              onChange={(e) => setStory(e.target.value)}
              rows={5}
              autoFocus
              placeholder="As a shopper, I want to apply a discount code at checkout so that the order total reflects the discount…"
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={!story.trim() || mutation.isPending}>
              {mutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Sparkles className="h-4 w-4 mr-1.5" />}
              Generate
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ScenarioDetailPanel({ projectId, scenario }: { projectId: string; scenario: Scenario }) {
  const queryClient = useQueryClient();
  const [editorOpen, setEditorOpen] = useState(false);
  const [runError, setRunError] = useState<string | null>(null);
  const [liveSessionId, setLiveSessionId] = useState<string | null>(null);
  const [liveMode, setLiveMode] = useState<'explore' | 'run'>('explore');
  const source = sourceMeta[scenario.source] ?? { label: scenario.source, variant: 'outline' as const };

  const runQuery = useQuery({
    queryKey: ['scenario-run', projectId, scenario.id],
    queryFn: () => getLatestScenarioRun(projectId, scenario.id),
    refetchInterval: (query) => {
      const s = query.state.data?.status;
      return s === 'Pending' || s === 'Running' ? 2500 : false;
    },
  });
  const run = runQuery.data;
  const running = run?.status === 'Pending' || run?.status === 'Running';

  const exploreMutation = useMutation({
    mutationFn: () => exploreScenario(projectId, scenario.id, getSelectedEnvironmentId() || undefined),
    onSuccess: (session) => {
      setRunError(null);
      setLiveMode('explore');
      setLiveSessionId(session.id);
      queryClient.invalidateQueries({ queryKey: ['scenario-run', projectId, scenario.id] });
    },
    onError: (e) => setRunError(getErrorMessage(e)),
  });

  const runMutation = useMutation({
    mutationFn: () => runScenario(projectId, scenario.id, getSelectedEnvironmentId() || undefined),
    onSuccess: (session) => {
      setRunError(null);
      setLiveMode('run');
      setLiveSessionId(session.id);
      queryClient.invalidateQueries({ queryKey: ['scenario-run', projectId, scenario.id] });
    },
    onError: (e) => setRunError(getErrorMessage(e)),
  });

  const busy = running || exploreMutation.isPending || runMutation.isPending;

  const outcomeVariant = run
    ? run.failedSteps > 0 ? 'destructive' : run.healedSteps > 0 ? 'warning' : 'success'
    : 'secondary';

  return (
    <div className="flex flex-col min-h-0 flex-1">
      {/* Toolbar — Explore / Run sit above the steps */}
      <div className="px-4 py-3 border-b border-border bg-muted/40">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h3 className="text-sm font-semibold text-foreground truncate flex items-center gap-2">
              <span className="h-2 w-2 rounded-full shrink-0" style={{ background: typeColor[scenario.type] ?? '#94a3b8' }} />
              {scenario.title}
            </h3>
            <div className="flex flex-wrap items-center gap-1.5 mt-1.5">
              <Badge variant={source.variant}>{source.label}</Badge>
              <Badge>{scenario.type}</Badge>
              <Badge variant="outline">Priority: {scenario.priority}</Badge>
              <Badge variant="outline">Risk: {scenario.risk}</Badge>
              {scenario.jiraKey && <Badge variant="secondary" className="font-mono">{scenario.jiraKey}</Badge>}
            </div>
          </div>
          <div className="flex items-center gap-1.5 shrink-0">
            <Button
              variant="outline" size="sm" className="h-8"
              disabled={busy}
              onClick={() => exploreMutation.mutate()}
              title="Explore the app to discover & ground this scenario's real steps"
            >
              {running || exploreMutation.isPending
                ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                : <Compass className="h-3.5 w-3.5 mr-1" />}
              {running ? 'Exploring…' : run ? 'Re-explore' : 'Explore'}
            </Button>
            <Button
              variant="outline" size="sm" className="h-8"
              disabled={busy || scenario.steps.length === 0}
              onClick={() => runMutation.mutate()}
              title={scenario.steps.length === 0 ? 'Add or ground steps first (Explore), then Run' : "Run this scenario's saved steps as a test"}
            >
              {runMutation.isPending
                ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                : <Play className="h-3.5 w-3.5 mr-1" />}
              Run
            </Button>
            <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Edit scenario" onClick={() => setEditorOpen(true)}>
              <Edit2 className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {runError && <Alert severity="error" className="text-xs">{runError}</Alert>}

        {scenario.preconditions && (
          <p className="text-xs text-muted-foreground">
            <strong>Preconditions:</strong> {scenario.preconditions}
          </p>
        )}

        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-1.5">Steps</div>
          {scenario.steps.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No steps yet. Use <strong>Explore</strong> to ground real steps from the app, or edit the scenario to add them.
            </p>
          ) : (
            <ol className="space-y-1">
              {scenario.steps.map((step, idx) => {
                const result = run?.steps.find((r) => r.stepOrder === idx + 1);
                return (
                  <li key={step.order} className="text-sm flex items-start gap-2">
                    <span className="mt-0.5"><StepStatusIcon status={result?.status} /></span>
                    <span className="flex-1">
                      <span className="text-muted-foreground mr-1">{idx + 1}.</span>
                      {step.action}
                      {step.expectedResult && (
                        <span className="text-xs text-muted-foreground ml-2">→ {step.expectedResult}</span>
                      )}
                      {result?.detail && (
                        <span className={`block text-xs mt-0.5 ${result.status === 'Failed' ? 'text-red-600' : result.status === 'Healed' ? 'text-amber-600' : 'text-muted-foreground'}`}>
                          {result.detail}
                        </span>
                      )}
                    </span>
                  </li>
                );
              })}
            </ol>
          )}
        </div>

        {run && (
          <div className="flex items-center gap-2 pt-2 border-t border-border">
            <Badge variant={outcomeVariant as 'success' | 'warning' | 'destructive' | 'secondary'}>{run.outcome}</Badge>
            <span className="text-xs text-muted-foreground">
              {run.passedSteps} passed · {run.healedSteps} healed · {run.failedSteps} failed
            </span>
          </div>
        )}

        {scenario.expectedResult && (
          <p className="text-sm"><strong>Expected:</strong> {scenario.expectedResult}</p>
        )}

        {scenario.tags.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {scenario.tags.map((tag) => <Badge key={tag} variant="outline" className="text-xs">{tag}</Badge>)}
          </div>
        )}
      </div>

      {editorOpen && (
        <ScenarioEditorDialog scenario={scenario} projectId={projectId} open={editorOpen} onClose={() => setEditorOpen(false)} />
      )}

      {liveSessionId && (
        <LiveExplorationDialog
          sessionId={liveSessionId}
          projectId={projectId}
          title={scenario.title}
          mode={liveMode}
          open={!!liveSessionId}
          onClose={() => {
            setLiveSessionId(null);
            queryClient.invalidateQueries({ queryKey: ['scenario-run', projectId, scenario.id] });
            queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
          }}
        />
      )}
    </div>
  );
}

function StepStatusIcon({ status }: { status?: StepRunStatus }) {
  switch (status) {
    case 'Passed': return <CheckCircle2 className="h-4 w-4 text-green-600" />;
    case 'Healed': return <Wrench className="h-4 w-4 text-amber-600" />;
    case 'Failed': return <XCircle className="h-4 w-4 text-red-600" />;
    case 'Skipped': return <MinusCircle className="h-4 w-4 text-gray-400" />;
    default: return <span className="inline-block h-4 w-4 rounded-full border border-gray-300" />;
  }
}

function SuiteEditorDialog({
  projectId, suite, onClose, onSaved,
}: {
  projectId: string;
  suite: TestSuite | null;
  onClose: () => void;
  onSaved: (saved?: TestSuite) => void;
}) {
  const [name, setName] = useState(suite?.name ?? '');
  const [description, setDescription] = useState(suite?.description ?? '');
  const [selected, setSelected] = useState<string[]>(suite?.scenarios.map((s) => s.scenarioId) ?? []);
  const [search, setSearch] = useState('');
  const [error, setError] = useState<string | null>(null);

  const { data: scenarios = [] } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const toggle = (id: string) =>
    setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));

  const filteredScenarios = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return scenarios;
    return scenarios.filter((s) => s.title.toLowerCase().includes(q));
  }, [scenarios, search]);

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = { projectId, name: name.trim(), description: description.trim() || undefined, scenarioIds: selected };
      return suite
        ? updateTestSuite({ id: suite.id, ...payload })
        : createTestSuite(payload);
    },
    onSuccess: (saved) => onSaved(saved),
    onError: (e) => setError(getErrorMessage(e)),
  });

  const orderIndex = (id: string) => selected.indexOf(id);

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-xl gap-0 p-0 overflow-hidden">
        <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="flex max-h-[85vh] flex-col">
          <DialogHeader className="border-b border-border px-6 py-4">
            <DialogTitle className="flex items-center gap-2">
              <Layers className="h-4 w-4 text-violet-500" />
              {suite ? 'Edit suite' : 'New suite'}
            </DialogTitle>
            <p className="text-sm text-muted-foreground">
              Group scenarios into a suite. They run sequentially in the order you select them.
            </p>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
            {error && <Alert severity="error" className="text-sm">{error}</Alert>}

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Name</Label>
                <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Checkout regression" required autoFocus />
              </div>
              <div className="space-y-1.5">
                <Label>Description <span className="text-muted-foreground font-normal">(optional)</span></Label>
                <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What this suite covers…" />
              </div>
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Scenarios</Label>
                <Badge variant={selected.length > 0 ? 'default' : 'secondary'}>{selected.length} selected</Badge>
              </div>

              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search scenarios…"
                  className="pl-8"
                />
              </div>

              <div className="rounded-lg border border-border overflow-hidden">
                <div className="max-h-72 overflow-y-auto divide-y divide-border">
                  {scenarios.length === 0 ? (
                    <p className="p-4 text-sm text-muted-foreground text-center">No scenarios in this project yet.</p>
                  ) : filteredScenarios.length === 0 ? (
                    <p className="p-4 text-sm text-muted-foreground text-center">No scenarios match &ldquo;{search}&rdquo;.</p>
                  ) : (
                    filteredScenarios.map((s: Scenario) => {
                      const idx = orderIndex(s.id);
                      const isSelected = idx >= 0;
                      return (
                        <button type="button" key={s.id} onClick={() => toggle(s.id)}
                          className={cn('flex w-full items-center gap-2.5 px-3 py-2.5 text-left text-sm transition-colors hover:bg-muted', isSelected && 'bg-violet-50 hover:bg-violet-50')}>
                          <span className={cn(
                            'flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-[10px] font-semibold',
                            isSelected ? 'border-violet-500 bg-violet-500 text-white' : 'border-gray-300 text-transparent',
                          )}>
                            {isSelected ? idx + 1 : ''}
                          </span>
                          <span className="h-2 w-2 rounded-full shrink-0" style={{ background: typeColor[s.type] ?? '#94a3b8' }} />
                          <span className={cn('truncate flex-1', isSelected && 'font-medium text-violet-700')}>{s.title}</span>
                          <Badge variant="outline" className="shrink-0 text-[10px]">{s.steps.length} steps</Badge>
                        </button>
                      );
                    })
                  )}
                </div>
              </div>
              <p className="text-xs text-muted-foreground">Numbered badges show the run order. Click a selected scenario again to remove it.</p>
            </div>
          </div>

          <DialogFooter className="border-t border-border px-6 py-4">
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
