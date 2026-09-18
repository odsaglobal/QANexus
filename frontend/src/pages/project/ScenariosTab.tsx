import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Sparkles, Edit2, Loader2, FileUp, Plus, Compass, Search,
  CheckCircle2, XCircle, Wrench, MinusCircle, Play, FileText, FileWarning, Layers,
  Trash2, Pencil, RefreshCw, Square, Radio, Check, X,
} from 'lucide-react';
import type { Scenario, TestSuite, StepRunStatus, ExplorationSession, StepCheckResult } from '../../api/types';
import { generateScenariosFromStory, listScenarios, exploreScenario, runScenario, getLatestScenarioRun, deleteScenario, deleteScenarios } from '../../api/scenarios';
import { cancelExploration, listExplorationSessions } from '../../api/explorer';
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
import { PageHeader } from '../../components/PageHeader';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
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

const sourceColor: Record<string, string> = {
  AiGenerated: '#7c3aed', Manual: '#0ea5e9', TestRail: '#16a34a',
};

const priorityColor: Record<string, string> = {
  Low: '#64748b', Medium: '#f59e0b', High: '#f97316', Critical: '#ef4444',
};

const sourceMeta: Record<string, { label: string; variant: 'default' | 'secondary' | 'outline' }> = {
  AiGenerated: { label: 'AI', variant: 'default' },
  Manual: { label: 'Manual', variant: 'secondary' },
  TestRail: { label: 'TestRail', variant: 'outline' },
};

/**
 * The three things a suite run can be narrowed by — the same attributes shown on every scenario row,
 * so what the user filters by is what they can see.
 */
const FACET_KEYS = ['types', 'priorities', 'tags'] as const;
type FacetKey = (typeof FACET_KEYS)[number];

const FACET_LABELS: Record<FacetKey, string> = {
  types: 'Type',
  priorities: 'Priority',
  tags: 'Tags',
};

/** Severity order, so Critical never sorts after Low. Values outside the list fall to the end. */
const FACET_ORDER: Record<FacetKey, string[]> = {
  types: ['Positive', 'Negative', 'Boundary', 'Smoke', 'Regression', 'Security', 'Accessibility', 'Api'],
  priorities: ['Critical', 'High', 'Medium', 'Low'],
  tags: [],
};

/** Chip colour matches the pill colour used on the scenario rows for the same value. */
const FACET_COLOR: Record<FacetKey, (value: string) => string | undefined> = {
  types: (v) => typeColor[v],
  priorities: (v) => priorityColor[v],
  tags: () => undefined,
};

function ColorPill({ label, color }: { label: string; color: string }) {
  return (
    <span
      className="inline-flex items-center rounded-full px-1.5 py-0.5 text-[10px] font-semibold"
      style={{ backgroundColor: `${color}1a`, color }}
    >
      {label}
    </span>
  );
}

/**
 * Checkbox drawn as a span, not an <input>: every place it is used already sits inside a <button>,
 * and nesting a form control in a button is invalid HTML. The parent button owns the click and the
 * aria-pressed state, so this is purely visual.
 */
function CheckBox({ checked, indeterminate, className }: {
  checked: boolean;
  indeterminate?: boolean;
  className?: string;
}) {
  const on = checked || indeterminate;
  return (
    <span aria-hidden className={cn(
      'flex h-4 w-4 shrink-0 items-center justify-center rounded border transition-colors',
      on ? 'border-violet-500 bg-violet-500 text-white' : 'border-input bg-background',
      className,
    )}>
      {indeterminate
        ? <span className="h-0.5 w-2 rounded-full bg-current" />
        : checked && <Check className="h-3 w-3" strokeWidth={3} />}
    </span>
  );
}

const ALL_SCENARIOS = '__all__';
// Test cases belonging to no suite. Without a bucket for them they were unreachable except by
// searching for a title you already knew — which is exactly how generated scenarios get forgotten.
const UNASSIGNED = '__unassigned__';

export function ScenariosTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selectedSuiteId, setSelectedSuiteId] = useState<string>(ALL_SCENARIOS);
  const [selectedScenarioId, setSelectedScenarioId] = useState<string | null>(null);
  // Ticked test cases, for bulk delete. Kept as ids (not indexes) so it survives filtering and re-sorting.
  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set());
  const [scenarioSearch, setScenarioSearch] = useState('');
  const [storyOpen, setStoryOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [manualScenarioOpen, setManualScenarioOpen] = useState(false);
  const [testRailOpen, setTestRailOpen] = useState(false);
  const [suiteEditorOpen, setSuiteEditorOpen] = useState(false);
  const [editingSuite, setEditingSuite] = useState<TestSuite | null>(null);
  const [runSuiteFor, setRunSuiteFor] = useState<TestSuite | null>(null);
  // The suite run being watched. Held here rather than in the row so it survives the list re-rendering
  // as the poll refreshes, and so it can be reopened after the dialog is closed.
  const [liveSuite, setLiveSuite] = useState<{ sessionId: string; name: string } | null>(null);
  const [suitesW, setSuitesW] = useState(240);
  const [scenariosW, setScenariosW] = useState(300);

  const startResize = (
    e: React.PointerEvent,
    current: number,
    setter: (n: number) => void,
    min: number,
    max: number,
  ) => {
    e.preventDefault();
    const startX = e.clientX;
    const startW = current;
    const onMove = (ev: PointerEvent) =>
      setter(Math.min(max, Math.max(min, startW + ev.clientX - startX)));
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  };

  const { data: scenarios = [], isLoading: scenariosLoading } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const { data: suites = [], isLoading: suitesLoading } = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
  });

  // Which scenarios are being explored/run RIGHT NOW. One poll answers this for every row at once,
  // and — unlike per-scenario run state — it survives closing the live dialog or leaving the tab, so a
  // run started anywhere (or by a teammate) still shows up here and can be stopped.
  const { data: activeSessions = [] } = useQuery({
    queryKey: ['exploration-sessions', projectId, 'active'],
    queryFn: () => listExplorationSessions(projectId, { activeOnly: true }),
    refetchInterval: 3000,
  });

  const activeByScenario = useMemo(() => {
    const map = new Map<string, ExplorationSession>();
    for (const s of activeSessions) {
      if (s.scenarioId) map.set(s.scenarioId, s);
    }
    return map;
  }, [activeSessions]);

  // Same poll, indexed by suite: a suite run is one session covering many scenarios, so it never
  // appears in the per-scenario map above and the suite row would otherwise look idle while running.
  const activeBySuite = useMemo(() => {
    const map = new Map<string, ExplorationSession>();
    for (const s of activeSessions) {
      if (s.suiteId) map.set(s.suiteId, s);
    }
    return map;
  }, [activeSessions]);

  const scenarioById = useMemo(
    () => new Map(scenarios.map((s) => [s.id, s])),
    [scenarios],
  );

  // Which suites each test case belongs to. Drives both the "Not in a suite" bucket and the suite
  // chips on the rows, so coverage is visible without opening every suite in turn.
  const suitesByScenario = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const suite of suites) {
      for (const ts of suite.scenarios) {
        const names = map.get(ts.scenarioId);
        if (names) names.push(suite.name);
        else map.set(ts.scenarioId, [suite.name]);
      }
    }
    return map;
  }, [suites]);

  const unassignedCount = useMemo(
    () => scenarios.reduce((n, s) => (suitesByScenario.has(s.id) ? n : n + 1), 0),
    [scenarios, suitesByScenario],
  );

  const listScenariosForColumn: Scenario[] = useMemo(() => {
    if (selectedSuiteId === ALL_SCENARIOS) return scenarios;
    if (selectedSuiteId === UNASSIGNED) return scenarios.filter((s) => !suitesByScenario.has(s.id));
    const suite = suites.find((s) => s.id === selectedSuiteId);
    if (!suite) return [];
    return [...suite.scenarios]
      .sort((a, b) => a.order - b.order)
      .map((ts) => scenarioById.get(ts.scenarioId))
      .filter((s): s is Scenario => Boolean(s));
  }, [selectedSuiteId, suites, scenarios, scenarioById, suitesByScenario]);

  const scenarioQuery = scenarioSearch.trim().toLowerCase();
  const visibleScenarios: Scenario[] = useMemo(() => {
    if (!scenarioQuery) return listScenariosForColumn;
    const terms = scenarioQuery.split(/\s+/);
    return listScenariosForColumn.filter((s) => {
      const hay = `${s.title} ${s.type} ${s.priority} ${s.source} ${s.tags.join(' ')}`.toLowerCase();
      return terms.every((t) => hay.includes(t));
    });
  }, [listScenariosForColumn, scenarioQuery]);

  // Keep the selected scenario valid; auto-select the first visible one when the current selection
  // is no longer in view (suite switched / filtered out), and clear it when nothing is listed.
  useEffect(() => {
    if (selectedScenarioId && !scenarioById.has(selectedScenarioId)) {
      setSelectedScenarioId(null);
      return;
    }
    const inView = selectedScenarioId ? visibleScenarios.some((s) => s.id === selectedScenarioId) : false;
    if (!inView) {
      setSelectedScenarioId(visibleScenarios[0]?.id ?? null);
    }
  }, [visibleScenarios, selectedScenarioId, scenarioById]);

  const selectedScenario = selectedScenarioId && visibleScenarios.some((s) => s.id === selectedScenarioId)
    ? scenarioById.get(selectedScenarioId) ?? null
    : null;

  // Only ticks for rows the user can currently see count: acting on a hidden selection is how people
  // delete things they never looked at.
  const checkedVisible = useMemo(
    () => visibleScenarios.filter((s) => checkedIds.has(s.id)),
    [visibleScenarios, checkedIds],
  );
  const allVisibleChecked = visibleScenarios.length > 0 && checkedVisible.length === visibleScenarios.length;

  const toggleChecked = (id: string) => {
    setCheckedIds((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  };

  const toggleAllVisible = () => {
    setCheckedIds((prev) => {
      const next = new Set(prev);
      if (allVisibleChecked) visibleScenarios.forEach((s) => next.delete(s.id));
      else visibleScenarios.forEach((s) => next.add(s.id));
      return next;
    });
  };

  // Drop ticks for test cases that no longer exist, so a stale id can never end up in a delete payload.
  useEffect(() => {
    setCheckedIds((prev) => {
      if (prev.size === 0) return prev;
      const next = new Set([...prev].filter((id) => scenarioById.has(id)));
      return next.size === prev.size ? prev : next;
    });
  }, [scenarioById]);

  const afterScenariosDeleted = (count: number) => {
    setCheckedIds(new Set());
    queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
    queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
    queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
    setNotice(`Deleted ${count} test case${count === 1 ? '' : 's'}.`);
  };

  const deleteScenarioMutation = useMutation({
    mutationFn: (id: string) => deleteScenario(projectId, id),
    onSuccess: () => afterScenariosDeleted(1),
    onError: (e) => setError(getErrorMessage(e)),
  });

  const bulkDeleteMutation = useMutation({
    mutationFn: (ids: string[]) => deleteScenarios(projectId, ids),
    onSuccess: (deleted) => afterScenariosDeleted(deleted),
    onError: (e) => setError(getErrorMessage(e)),
  });

  const deleting = deleteScenarioMutation.isPending || bulkDeleteMutation.isPending;

  const requestDeleteScenario = async (scenario: Scenario) => {
    setError(null);
    setNotice(null);
    const inSuites = suitesByScenario.get(scenario.id);
    if (await confirm({
      title: 'Delete test case?',
      description: inSuites
        ? `"${scenario.title}" will be removed, including from ${inSuites.length === 1 ? 'the suite' : `${inSuites.length} suites`} it belongs to.`
        : `"${scenario.title}" will be removed.`,
      confirmText: 'Delete',
      tone: 'destructive',
    })) {
      deleteScenarioMutation.mutate(scenario.id);
    }
  };

  const requestBulkDelete = async () => {
    setError(null);
    setNotice(null);
    const ids = checkedVisible.map((s) => s.id);
    if (ids.length === 0) return;
    if (await confirm({
      title: `Delete ${ids.length} test case${ids.length === 1 ? '' : 's'}?`,
      description: 'They will be removed from the project and from any suites they belong to.',
      confirmText: `Delete ${ids.length}`,
      tone: 'destructive',
    })) {
      bulkDeleteMutation.mutate(ids);
    }
  };

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
      <PageHeader
        title="Scenario Management"
        description="Generate manual test cases from a story (grounded in your Business Context docs), add them by hand, or import CSV/XLSX — all editable with ordered steps."
        actions={(
          <>
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
          </>
        )}
      />
      {error && <Alert severity="error" className="text-sm">{error}</Alert>}
      {notice && <Alert severity="success" className="text-sm">{notice}</Alert>}

      {/* TestRail-style three-pane: Suites · Scenarios · Steps */}
      <div className="flex rounded-lg border border-border overflow-hidden h-[calc(100vh-20rem)] min-h-[480px]">
        {/* Column 1 — Suites */}
        <div className="relative shrink-0 border-r border-border flex flex-col min-h-0" style={{ width: suitesW }}>
          <div className="px-3 py-2 border-b border-border bg-muted/40 flex items-center gap-2">
            <Layers className="h-4 w-4 text-violet-500" />
            <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Library</span>
            <Button size="icon" variant="ghost" className="ml-auto h-7 w-7" aria-label="New suite"
              onClick={() => { setEditingSuite(null); setSuiteEditorOpen(true); }}>
              <Plus className="h-4 w-4" />
            </Button>
          </div>
          <div className="flex-1 overflow-y-auto p-1.5 space-y-0.5">
            {(suitesLoading || scenariosLoading) && (
              <div className="py-6 text-center text-muted-foreground text-xs">
                <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading…
              </div>
            )}

            {/* Every test case is reachable from here: the whole library, the ones no suite covers,
                then the suites themselves. */}
            <button
              type="button"
              onClick={() => setSelectedSuiteId(ALL_SCENARIOS)}
              className={cn(
                'flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm transition-colors',
                selectedSuiteId === ALL_SCENARIOS ? 'bg-violet-50' : 'hover:bg-muted',
              )}
            >
              <FileText className={cn('h-4 w-4 shrink-0', selectedSuiteId === ALL_SCENARIOS ? 'text-violet-500' : 'text-muted-foreground')} />
              <span className={cn('flex-1 truncate', selectedSuiteId === ALL_SCENARIOS && 'text-violet-700 font-medium')}>
                All test cases
              </span>
              <Badge variant="secondary">{scenarios.length}</Badge>
            </button>
            <button
              type="button"
              onClick={() => setSelectedSuiteId(UNASSIGNED)}
              title="Test cases that no suite includes — they never run as part of a suite"
              className={cn(
                'flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm transition-colors',
                selectedSuiteId === UNASSIGNED ? 'bg-violet-50' : 'hover:bg-muted',
              )}
            >
              <FileWarning className={cn('h-4 w-4 shrink-0', selectedSuiteId === UNASSIGNED ? 'text-violet-500' : 'text-muted-foreground')} />
              <span className={cn('flex-1 truncate', selectedSuiteId === UNASSIGNED && 'text-violet-700 font-medium')}>
                Not in a suite
              </span>
              <Badge variant={unassignedCount > 0 ? 'warning' : 'secondary'}>{unassignedCount}</Badge>
            </button>

            <div className="flex items-center gap-2 px-2.5 pb-1 pt-3">
              <span className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">Suites</span>
              <span className="h-px flex-1 bg-border" />
              <span className="text-[10px] text-muted-foreground">{suites.length}</span>
            </div>

            {!suitesLoading && suites.length === 0 && (
              <p className="px-2.5 py-2 text-xs text-muted-foreground">
                No suites yet. Use + to group test cases into a suite.
              </p>
            )}

            {suites.map((suite) => {
              const active = selectedSuiteId === suite.id;
              const suiteSession = activeBySuite.get(suite.id);
              return (
                <div
                  key={suite.id}
                  className={cn(
                    'group flex items-center gap-1 rounded-md pl-2.5 pr-1 transition-colors',
                    active ? 'bg-violet-50' : suiteSession ? 'bg-violet-50/40' : 'hover:bg-muted',
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
                  {/* Always visible: these are the primary actions on a suite, and hiding them behind
                      hover made "run" undiscoverable and unusable on touch. */}
                  <div className="flex items-center">
                    {suiteSession ? (
                      <Button
                        size="icon" className="h-7 w-7 text-violet-600 hover:bg-violet-100 hover:text-violet-700" variant="ghost"
                        title="This suite is running — watch it live" aria-label={`Watch ${suite.name} run`}
                        onClick={(e) => { e.stopPropagation(); setLiveSuite({ sessionId: suiteSession.id, name: suite.name }); }}
                      >
                        <Radio className="h-3.5 w-3.5 animate-pulse" />
                      </Button>
                    ) : (
                      <Button
                        size="icon" className="h-7 w-7 text-emerald-600 hover:bg-emerald-50 hover:text-emerald-700" variant="ghost"
                        title={suite.scenarioCount === 0 ? 'Add scenarios to this suite before running it' : 'Run suite'}
                        aria-label={`Run ${suite.name}`}
                        disabled={suite.scenarioCount === 0}
                        onClick={(e) => { e.stopPropagation(); setError(null); setRunSuiteFor(suite); }}
                      >
                        <Play className="h-3.5 w-3.5" />
                      </Button>
                    )}
                    <Button
                      size="icon" className="h-7 w-7 text-sky-600 hover:bg-sky-50 hover:text-sky-700" variant="ghost"
                      aria-label={`Edit ${suite.name}`}
                      onClick={(e) => { e.stopPropagation(); setEditingSuite(suite); setSuiteEditorOpen(true); }}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      size="icon" className="h-7 w-7 text-rose-500 hover:bg-rose-50 hover:text-rose-600" variant="ghost"
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
          <div
            role="separator"
            onPointerDown={(e) => startResize(e, suitesW, setSuitesW, 180, 420)}
            className="absolute top-0 right-0 z-20 h-full w-1.5 -mr-0.5 cursor-col-resize hover:bg-violet-300/60 active:bg-violet-400/70"
          />
        </div>

        {/* Column 2 — Scenarios */}
        <div className="relative shrink-0 border-r border-border flex flex-col min-h-0" style={{ width: scenariosW }}>
          <div className="px-3 py-2 border-b border-border bg-muted/40 flex items-center gap-2">
            <button
              type="button"
              onClick={toggleAllVisible}
              disabled={visibleScenarios.length === 0}
              aria-pressed={allVisibleChecked}
              aria-label={allVisibleChecked ? 'Clear selection' : 'Select all listed test cases'}
              title={allVisibleChecked ? 'Clear selection' : 'Select all listed test cases'}
              className="flex h-5 w-5 items-center justify-center disabled:opacity-40"
            >
              <CheckBox
                checked={allVisibleChecked}
                indeterminate={!allVisibleChecked && checkedVisible.length > 0}
              />
            </button>
            <FileText className="h-4 w-4 text-violet-500" />
            <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground truncate">
              {selectedSuiteId === ALL_SCENARIOS ? 'All test cases'
                : selectedSuiteId === UNASSIGNED ? 'Not in a suite'
                : suites.find((s) => s.id === selectedSuiteId)?.name ?? 'Test cases'}
            </span>
            <Badge variant="secondary" className="ml-auto">{visibleScenarios.length}</Badge>
          </div>
          <div className="px-2 py-1.5 border-b border-border">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={scenarioSearch}
                onChange={(e) => setScenarioSearch(e.target.value)}
                placeholder="Search scenarios…"
                className="h-8 pl-8 text-xs"
              />
            </div>
          </div>
          {checkedVisible.length > 0 && (
            <div className="flex items-center gap-2 border-b border-border bg-violet-50 px-2.5 py-1.5">
              <span className="text-xs font-medium text-violet-700">
                {checkedVisible.length} selected
              </span>
              <button
                type="button"
                onClick={() => setCheckedIds(new Set())}
                className="text-xs text-muted-foreground underline-offset-2 hover:underline"
              >
                Clear
              </button>
              <Button
                size="sm" variant="ghost" disabled={deleting}
                className="ml-auto h-7 px-2 text-rose-600 hover:bg-rose-50 hover:text-rose-700"
                onClick={requestBulkDelete}
              >
                {bulkDeleteMutation.isPending
                  ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                  : <Trash2 className="h-3.5 w-3.5 mr-1" />}
                Delete
              </Button>
            </div>
          )}
          <div className="flex-1 overflow-y-auto p-1.5 space-y-0.5">
            {scenariosLoading && (
              <div className="py-6 text-center text-muted-foreground text-xs">
                <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading scenarios…
              </div>
            )}
            {!scenariosLoading && visibleScenarios.length === 0 && (
              <p className="px-2.5 py-3 text-xs text-muted-foreground">
                {scenarioQuery
                  ? `No test cases match “${scenarioSearch}”.`
                  : selectedSuiteId === ALL_SCENARIOS
                    ? 'No test cases yet. Generate with AI, import a CSV/XLSX, or sync from TestRail.'
                    : selectedSuiteId === UNASSIGNED
                      ? 'Every test case belongs to a suite.'
                      : 'This suite has no test cases. Edit the suite to add some.'}
              </p>
            )}
            {visibleScenarios.map((scenario) => {
              const active = selectedScenarioId === scenario.id;
              const checked = checkedIds.has(scenario.id);
              const source = sourceMeta[scenario.source] ?? { label: scenario.source, variant: 'outline' as const };
              const liveSession = activeByScenario.get(scenario.id);
              // Membership only needs spelling out when the list isn't already one suite's contents.
              const memberOf = selectedSuiteId === ALL_SCENARIOS ? suitesByScenario.get(scenario.id) : undefined;
              return (
                <div
                  key={scenario.id}
                  className={cn(
                    'group flex items-start gap-1.5 rounded-md pl-2 pr-1 transition-colors',
                    active ? 'bg-violet-50' : checked ? 'bg-violet-50/60' : 'hover:bg-muted',
                    liveSession && !active && !checked && 'bg-violet-50/40',
                  )}
                >
                  <button
                    type="button"
                    onClick={() => toggleChecked(scenario.id)}
                    aria-pressed={checked}
                    aria-label={`${checked ? 'Deselect' : 'Select'} ${scenario.title}`}
                    className="flex h-5 w-5 shrink-0 items-center justify-center self-center"
                  >
                    <CheckBox checked={checked} />
                  </button>
                  <button
                    type="button"
                    onClick={() => setSelectedScenarioId(scenario.id)}
                    className="flex min-w-0 flex-1 flex-col gap-1 py-2 text-left"
                  >
                    <div className="flex items-center gap-2">
                      <span className={cn('flex-1 truncate text-sm', active && 'text-violet-700 font-medium')}>{scenario.title}</span>
                      {liveSession && (
                        <span
                          className="inline-flex shrink-0 items-center gap-1 rounded-full bg-violet-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-violet-700"
                          title="This scenario is running now — open it to watch live or stop it"
                        >
                          <span className="h-1.5 w-1.5 rounded-full bg-violet-600 animate-pulse" />
                          Live
                        </span>
                      )}
                      <Badge variant="outline" className="shrink-0 text-[10px]">{scenario.steps.length}</Badge>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <ColorPill label={source.label} color={sourceColor[scenario.source] ?? '#64748b'} />
                      <ColorPill label={scenario.type} color={typeColor[scenario.type] ?? '#94a3b8'} />
                      <ColorPill label={scenario.priority} color={priorityColor[scenario.priority] ?? '#64748b'} />
                    </div>
                    {selectedSuiteId === ALL_SCENARIOS && (
                      <div className="flex min-w-0 items-center gap-1 text-[10px] text-muted-foreground">
                        <Layers className="h-3 w-3 shrink-0" />
                        <span className="truncate" title={memberOf?.join(', ')}>
                          {memberOf ? memberOf.join(', ') : 'Not in a suite'}
                        </span>
                      </div>
                    )}
                  </button>
                  {/* Hidden until hover/focus so it cannot be hit by accident, but always reachable by keyboard. */}
                  <Button
                    size="icon" variant="ghost"
                    className="mt-1 h-7 w-7 shrink-0 text-rose-500 opacity-0 transition-opacity hover:bg-rose-50 hover:text-rose-600 focus-visible:opacity-100 group-hover:opacity-100"
                    aria-label={`Delete ${scenario.title}`}
                    title="Delete test case"
                    disabled={deleting || Boolean(liveSession)}
                    onClick={(e) => { e.stopPropagation(); void requestDeleteScenario(scenario); }}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              );
            })}
          </div>
          <div
            role="separator"
            onPointerDown={(e) => startResize(e, scenariosW, setScenariosW, 220, 560)}
            className="absolute top-0 right-0 z-20 h-full w-1.5 -mr-0.5 cursor-col-resize hover:bg-violet-300/60 active:bg-violet-400/70"
          />
        </div>

        {/* Column 3 — Selected scenario steps */}
        <div className="flex-1 min-w-0 flex flex-col min-h-0">
          {selectedScenario ? (
            <ScenarioDetailPanel
              key={selectedScenario.id}
              projectId={projectId}
              scenario={selectedScenario}
              activeSession={activeByScenario.get(selectedScenario.id) ?? null}
            />
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
          suites={suites}
          open={manualScenarioOpen}
          onClose={() => setManualScenarioOpen(false)}
          onCreated={(suiteId) => { setNotice('Test case added to the suite.'); setSelectedSuiteId(suiteId); }}
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
          onStarted={(session, suiteName) => {
            // Open the live view immediately. Previously the dialog just closed, so a started suite
            // gave no feedback at all until someone happened to look at the Executions page.
            setLiveSuite({ sessionId: session.id, name: suiteName });
            queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] });
          }}
        />
      )}
      {liveSuite && (
        <LiveExplorationDialog
          sessionId={liveSuite.sessionId}
          projectId={projectId}
          title={`Suite: ${liveSuite.name}`}
          mode="run"
          open
          onClose={() => {
            setLiveSuite(null);
            queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] });
          }}
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

function ScenarioDetailPanel({ projectId, scenario, activeSession }: {
  projectId: string;
  scenario: Scenario;
  activeSession?: ExplorationSession | null;
}) {
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
  // The session list is the more reliable signal: the latest-run record only appears once the agent has
  // written something, so a just-started exploration would otherwise look idle for its first few seconds.
  const running = Boolean(activeSession) || run?.status === 'Pending' || run?.status === 'Running';
  const liveId = activeSession?.id ?? (running ? run?.sessionId ?? null : null);

  const stopMutation = useMutation({
    mutationFn: () => cancelExploration(projectId, liveId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] });
      queryClient.invalidateQueries({ queryKey: ['scenario-run', projectId, scenario.id] });
    },
    onError: (e) => setRunError(getErrorMessage(e)),
  });

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
            <h3 className="text-sm font-semibold text-foreground truncate">
              {scenario.title}
            </h3>
            <div className="flex flex-wrap items-center gap-1.5 mt-1.5">
              <ColorPill label={source.label} color={sourceColor[scenario.source] ?? '#64748b'} />
              <ColorPill label={scenario.type} color={typeColor[scenario.type] ?? '#94a3b8'} />
              <ColorPill label={`Priority: ${scenario.priority}`} color={priorityColor[scenario.priority] ?? '#64748b'} />
              <Badge variant="outline">Risk: {scenario.risk}</Badge>
              {scenario.jiraKey && <Badge variant="secondary" className="font-mono">{scenario.jiraKey}</Badge>}
            </div>
          </div>
          <div className="flex items-center gap-1.5 shrink-0">
            {running && liveId && (
              <>
                <Button
                  size="sm" variant="outline"
                  className="h-8 border-violet-300 text-violet-700 hover:bg-violet-50"
                  onClick={() => setLiveSessionId(liveId)}
                  title="Watch this run in the live browser view"
                >
                  <Radio className="h-3.5 w-3.5 mr-1 animate-pulse" /> Watch live
                </Button>
                <Button
                  size="sm" variant="destructive" className="h-8"
                  disabled={stopMutation.isPending}
                  onClick={() => stopMutation.mutate()}
                  title="Stop this run"
                >
                  {stopMutation.isPending
                    ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                    : <Square className="h-3.5 w-3.5 mr-1" />}
                  {stopMutation.isPending ? 'Stopping…' : 'Stop'}
                </Button>
              </>
            )}
            <Button
              size="sm" className="h-8 bg-violet-600 text-white hover:bg-violet-700"
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
              size="sm" className="h-8 bg-green-600 text-white hover:bg-green-700 disabled:bg-green-600/50"
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
                    <div className="flex-1">
                      <span className="text-muted-foreground mr-1">{idx + 1}.</span>
                      {step.action}
                      {step.expectedResult && (
                        <ValidationTree
                          expectedResult={step.expectedResult}
                          status={result?.status}
                          checks={result?.checks}
                        />
                      )}
                      {/* The summary sentence just concatenates the failed actuals, which the tree
                          already shows per assertion — so it is only useful without a breakdown. */}
                      {result?.detail && !(result.checks && result.checks.length > 0) && (
                        <StepDetail
                          detail={result.detail}
                          className={`text-xs mt-0.5 ${result.status === 'Failed' ? 'text-red-600' : result.status === 'Healed' ? 'text-amber-600' : 'text-muted-foreground'}`}
                        />
                      )}
                    </div>
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

/**
 * Splits a step's expected result into its individual assertions.
 *
 * Only separators an author uses deliberately count: newlines, bullet markers and semicolons.
 * Splitting on " and " was rejected — it would shred targets like
 * 'Search for Products, Brands and More' into nonsense assertions.
 */
export function splitValidations(expectedResult: string): string[] {
  const parts = expectedResult
    .split(/\r?\n|;/)
    .map((line) => line.replace(/^\s*[-*\u2022]\s*/, '').trim())
    .filter((line) => line.length > 0);

  return parts.length > 0 ? parts : [expectedResult.trim()];
}

/**
 * Renders a step's assertions as a tree branching off the step, one tick per validation.
 *
 * Two modes. When the step's expectation has been compiled into machine-checkable assertions, each
 * leaf carries its OWN verdict and the concrete value read off the page — so a step that failed on
 * one condition no longer shows every condition as failed. Otherwise the expectation is split on the
 * author's own separators and the leaves share the step's verdict, which is all that can honestly be
 * said when the whole sentence was judged as one.
 */
export function ValidationTree({
  expectedResult,
  status,
  checks,
}: {
  expectedResult: string;
  status?: StepRunStatus;
  checks?: StepCheckResult[] | null;
}) {
  // Only the tick carries the verdict's colour. Tinting the assertion text as well shouted over the
  // step it belongs to and made a report of thirty green rows hard to actually read.
  const iconTone =
    status === 'Passed' ? 'text-green-600'
    : status === 'Healed' ? 'text-amber-600'
    : status === 'Failed' ? 'text-red-600'
    : 'text-muted-foreground';

  if (checks && checks.length > 0) {
    return (
      <ul className="mt-1 ml-[6px] space-y-1 border-l border-border pl-3">
        {checks.map((check, i) => (
          <li key={i} className="relative flex items-start gap-1.5">
            <span className="absolute -left-3 top-[9px] w-2 border-t border-border" />
            {check.passed
              ? <Check className="h-3.5 w-3.5 shrink-0 mt-px text-green-600" strokeWidth={2.5} />
              : <X className={`h-3.5 w-3.5 shrink-0 mt-px ${check.unresolved ? 'text-amber-600' : 'text-red-600'}`} strokeWidth={2.5} />}
            <div className="min-w-0 flex-1">
              <span className="text-xs text-foreground">{check.label}</span>
              {!check.passed && (
                // Expected and actual are only worth the space when they disagree; on a passing
                // check they would just restate the label.
                <dl className="mt-0.5 grid grid-cols-[auto_1fr] gap-x-2 gap-y-px text-[11px]">
                  {check.expected && (
                    <>
                      <dt className="text-muted-foreground">expected</dt>
                      <dd className="min-w-0 break-words text-foreground">{check.expected}</dd>
                    </>
                  )}
                  {check.actual && (
                    <>
                      <dt className="text-muted-foreground">actual</dt>
                      <dd className={`min-w-0 break-words ${check.unresolved ? 'text-amber-700' : 'text-red-600'}`}>
                        {check.actual}
                      </dd>
                    </>
                  )}
                </dl>
              )}
            </div>
          </li>
        ))}
      </ul>
    );
  }

  const validations = splitValidations(expectedResult);

  return (
    <ul className="mt-1 ml-[6px] space-y-1 border-l border-border pl-3">
      {validations.map((validation, i) => (
        <li key={i} className="relative flex items-start gap-1.5">
          {/* Elbow joining this leaf to the vertical rail. */}
          <span className="absolute -left-3 top-[9px] w-2 border-t border-border" />
          {status === 'Failed'
            ? <X className={`h-3.5 w-3.5 shrink-0 mt-px ${iconTone}`} strokeWidth={2.5} />
            : <Check className={`h-3.5 w-3.5 shrink-0 mt-px ${iconTone}`} strokeWidth={2.5} />}
          <span className="text-xs text-foreground">{validation}</span>
        </li>
      ))}
    </ul>
  );
}

/**
 * A step's outcome message. A failed step reads "Actual: <what the page did>" — the expectation is
 * already listed directly above it — so the label is pulled out as a tag and the observation left to
 * read as a plain sentence. Colour comes from the caller, since the report and the tab use different
 * palettes.
 */
export function StepDetail({ detail, className }: { detail: string; className?: string }) {
  const label = /^\s*actual\s*:\s*/i.exec(detail);

  if (!label) {
    return <span className={cn('block', className)}>{detail}</span>;
  }

  return (
    <span className={cn('block', className)}>
      <span className="mr-1.5 rounded bg-current/10 px-1 py-px text-[9px] font-semibold uppercase tracking-wide align-[1px]">
        Actual
      </span>
      {detail.slice(label[0].length)}
    </span>
  );
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
      <DialogContent className="w-[calc(100vw-2rem)] max-w-xl gap-0 p-0 overflow-hidden">
        <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="flex max-h-[85vh] min-w-0 w-full flex-col">
          <DialogHeader className="border-b border-border px-6 py-4 pr-12">
            <DialogTitle className="flex items-center gap-2">
              <Layers className="h-4 w-4 text-violet-500" />
              {suite ? 'Edit suite' : 'New suite'}
            </DialogTitle>
            <p className="text-sm text-muted-foreground">
              Group scenarios into a suite. They run sequentially in the order you select them.
            </p>
          </DialogHeader>

          <div className="flex-1 min-w-0 overflow-y-auto px-6 py-4 space-y-4">
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
                          className={cn('flex w-full min-w-0 items-center gap-2.5 px-3 py-2.5 text-left text-sm transition-colors hover:bg-muted', isSelected && 'bg-violet-50 hover:bg-violet-50')}>
                          <span className={cn(
                            'flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-[10px] font-semibold',
                            isSelected ? 'border-violet-500 bg-violet-500 text-white' : 'border-gray-300 text-transparent',
                          )}>
                            {isSelected ? idx + 1 : ''}
                          </span>
                          <span className="h-2 w-2 rounded-full shrink-0" style={{ background: typeColor[s.type] ?? '#94a3b8' }} />
                          <span className={cn('truncate flex-1 min-w-0', isSelected && 'font-medium text-violet-700')}>{s.title}</span>
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
  projectId, suite, onClose, onError, onStarted,
}: {
  projectId: string;
  suite: TestSuite;
  onClose: () => void;
  onError: (msg: string) => void;
  onStarted: (session: ExplorationSession, suiteName: string) => void;
}) {
  const [environmentId, setEnvironmentId] = useState<string>('');
  const [selected, setSelected] = useState<Record<FacetKey, string[]>>({
    types: [], priorities: [], tags: [],
  });

  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
  });

  // A suite row carries only id/title/order, so the filterable attributes come from the scenario
  // list — the same cached query the surrounding tab already uses.
  const { data: allScenarios = [] } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const defaultEnv = useMemo(
    () => environments.find((e) => e.isDefault) ?? environments[0],
    [environments],
  );
  const effectiveEnv = environmentId || defaultEnv?.id || '';

  // The numbering in this list is a promise about execution order, so sort by it rather than trusting
  // whatever order the API happened to return.
  const orderedScenarios = useMemo(
    () => [...suite.scenarios].sort((a, b) => a.order - b.order),
    [suite.scenarios],
  );

  const scenarioById = useMemo(
    () => new Map(allScenarios.map((s) => [s.id, s])),
    [allScenarios],
  );

  /** The values a scenario contributes to each facet — the same three things its row displays. */
  const facetValues = (scenarioId: string): Record<FacetKey, string[]> => {
    const s = scenarioById.get(scenarioId);
    return {
      types: s ? [s.type] : [],
      priorities: s ? [s.priority] : [],
      tags: s?.tags ?? [],
    };
  };

  // Only values present in THIS suite: offering the project's whole vocabulary would mostly offer
  // filters that select nothing.
  const facets = useMemo(() => {
    const counts: Record<FacetKey, Map<string, number>> = {
      types: new Map(), priorities: new Map(), tags: new Map(),
    };
    for (const s of orderedScenarios) {
      const values = facetValues(s.scenarioId);
      for (const key of FACET_KEYS) {
        for (const v of values[key]) counts[key].set(v, (counts[key].get(v) ?? 0) + 1);
      }
    }
    return FACET_KEYS.map((key) => ({
      key,
      label: FACET_LABELS[key],
      // Type and priority have a meaningful order (Critical before Low); tags are alphabetical.
      values: [...counts[key].entries()].sort((a, b) =>
        key === 'tags'
          ? a[0].localeCompare(b[0])
          : (FACET_ORDER[key].indexOf(a[0]) + 1 || 99) - (FACET_ORDER[key].indexOf(b[0]) + 1 || 99),
      ),
    })).filter((f) => f.values.length > 0);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orderedScenarios, scenarioById]);

  const activeCount = FACET_KEYS.reduce((n, key) => n + selected[key].length, 0);

  // OR within a facet, AND across facets — mirroring the server. Picking "Negative" and "Critical"
  // means critical negative tests, not "everything negative plus everything critical".
  const matchingScenarios = useMemo(() => {
    if (activeCount === 0) return orderedScenarios;
    return orderedScenarios.filter((s) => {
      const values = facetValues(s.scenarioId);
      return FACET_KEYS.every(
        (key) => selected[key].length === 0 || values[key].some((v) => selected[key].includes(v)),
      );
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orderedScenarios, selected, activeCount, scenarioById]);

  const toggleValue = (key: FacetKey, value: string) =>
    setSelected((prev) => ({
      ...prev,
      [key]: prev[key].includes(value) ? prev[key].filter((v) => v !== value) : [...prev[key], value],
    }));

  // Filters decide what is OFFERED; the checkboxes decide what actually runs. Tracking the
  // exclusions (rather than the inclusions) means a scenario the filter reveals later starts out
  // checked, which is what "I unticked these three" implies.
  const [unchecked, setUnchecked] = useState<Set<string>>(new Set());

  const selectedScenarios = useMemo(
    () => matchingScenarios.filter((s) => !unchecked.has(s.scenarioId)),
    [matchingScenarios, unchecked],
  );

  const toggleScenario = (scenarioId: string) =>
    setUnchecked((prev) => {
      const next = new Set(prev);
      if (!next.delete(scenarioId)) next.add(scenarioId);
      return next;
    });

  const allChecked = matchingScenarios.length > 0 && selectedScenarios.length === matchingScenarios.length;
  const someChecked = selectedScenarios.length > 0 && !allChecked;

  // Select-all only touches the rows currently listed; a scenario hidden by a filter keeps its state.
  const toggleAll = () =>
    setUnchecked((prev) => {
      const next = new Set(prev);
      for (const s of matchingScenarios) {
        if (allChecked) next.add(s.scenarioId);
        else next.delete(s.scenarioId);
      }
      return next;
    });

  const resetSelection = () => {
    setSelected({ types: [], priorities: [], tags: [] });
    setUnchecked(new Set());
  };

  const runMutation = useMutation({
    mutationFn: () => runTestSuite(projectId, suite.id, effectiveEnv || undefined, {
      ...selected,
      // Only pin explicit ids when something was actually unchecked — otherwise the run would be
      // frozen to today's suite membership instead of "whatever the filter matches".
      scenarioIds: allChecked ? undefined : selectedScenarios.map((s) => s.scenarioId),
    }),
    onSuccess: (session) => { onStarted(session, suite.name); onClose(); },
    onError: (e) => { onError(getErrorMessage(e)); onClose(); },
  });

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      {/* p-0 + gap-0: the header, the two panes and the footer own their padding so the divider rules
          can run edge to edge. */}
      <DialogContent className="w-[calc(100vw-2rem)] max-w-4xl gap-0 p-0">
        <DialogHeader className="border-b border-border px-6 py-4 text-left">
          <DialogTitle className="pr-8">Run suite</DialogTitle>
          <DialogDescription>
            <span className="font-medium text-foreground">{suite.name}</span>
            {' · '}
            {selectedScenarios.length} test{selectedScenarios.length === 1 ? '' : 's'} run one after
            another in a single browser session.
          </DialogDescription>
        </DialogHeader>

        {/* Config on the left, the resulting run order on the right, so the effect of a filter is
            visible in the same glance as the filter itself. Each pane scrolls on its own. */}
        <div className="grid min-h-0 grid-cols-1 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
          <div className="min-w-0 max-h-[62vh] space-y-5 overflow-y-auto border-b border-border px-6 py-5 md:border-b-0 md:border-r">
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

            {facets.length > 0 && (
              <div className="min-w-0 space-y-4">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <Label>Filters</Label>
                    <p className="mt-0.5 text-xs text-muted-foreground">
                      {activeCount === 0
                        ? 'None selected — the whole suite runs.'
                        : 'Any value within a group, and all groups together.'}
                    </p>
                  </div>
                  {activeCount > 0 && (
                    <button type="button"
                      onClick={resetSelection}
                      className="shrink-0 text-xs font-medium text-violet-600 hover:text-violet-700">
                      Clear all
                    </button>
                  )}
                </div>

              {facets.map((facet) => (
                <div key={facet.key} className="min-w-0 space-y-2">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                    {facet.label}
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {facet.values.map(([value, count]) => {
                      const active = selected[facet.key].includes(value);
                      const color = FACET_COLOR[facet.key](value);
                      return (
                        <button type="button" key={value} onClick={() => toggleValue(facet.key, value)}
                          aria-pressed={active}
                          className={cn(
                            'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition-colors',
                            active
                              ? 'border-violet-500 bg-violet-500 text-white shadow-sm'
                              : 'border-border bg-background text-muted-foreground hover:border-violet-300 hover:text-foreground',
                          )}>
                          {color && (
                            <span className="h-1.5 w-1.5 rounded-full"
                              style={{ background: active ? '#fff' : color }} />
                          )}
                          {value}
                          <span className={cn('tabular-nums', active ? 'text-violet-100' : 'text-muted-foreground/70')}>
                            {count}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              ))}
              </div>
            )}
          </div>

          <div className="flex min-h-0 min-w-0 flex-col bg-muted/30">
            <div className="flex items-center justify-between gap-2 border-b border-border px-6 py-3">
              <button type="button" onClick={toggleAll} disabled={matchingScenarios.length === 0}
                aria-label={allChecked ? 'Uncheck every test' : 'Check every test'}
                className="flex items-center gap-2 text-sm font-medium disabled:opacity-50">
                <CheckBox checked={allChecked} indeterminate={someChecked} />
                Run order
              </button>
              <span className="text-xs tabular-nums text-muted-foreground">
                {selectedScenarios.length} of {orderedScenarios.length} selected
              </span>
            </div>
            {matchingScenarios.length === 0 ? (
              <p className="px-6 py-10 text-center text-sm text-muted-foreground">
                No test in this suite matches the selected filters.
              </p>
            ) : (
              <ol className="min-h-0 max-h-[54vh] flex-1 divide-y divide-border overflow-y-auto overflow-x-hidden">
                {matchingScenarios.map((s) => {
                  const scenario = scenarioById.get(s.scenarioId);
                  const checked = !unchecked.has(s.scenarioId);
                  // Numbering follows the RUN, not the list, so it keeps matching the live view's
                  // "test 3 of 7" as rows are unchecked.
                  const position = checked ? selectedScenarios.findIndex((x) => x.scenarioId === s.scenarioId) + 1 : null;
                  return (
                    // min-w-0 on both the row and the title: without it the flex item refuses to
                    // shrink, so a long test name widens the pane instead of truncating.
                    <li key={s.scenarioId} className="min-w-0">
                      <button type="button" onClick={() => toggleScenario(s.scenarioId)}
                        aria-pressed={checked}
                        className={cn(
                          'flex w-full min-w-0 items-start gap-3 px-6 py-2.5 text-left transition-colors hover:bg-muted',
                          !checked && 'opacity-55',
                        )}>
                        <CheckBox checked={checked} className="mt-0.5" />
                        <span className="w-5 shrink-0 pt-0.5 text-right text-xs tabular-nums text-muted-foreground">
                          {position ?? '—'}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className={cn('block truncate text-sm', !checked && 'line-through')}
                            title={s.title ?? s.scenarioId}>
                            {s.title ?? s.scenarioId}
                          </span>
                          {scenario && (
                            <span className="mt-1 flex flex-wrap items-center gap-1.5">
                              <ColorPill label={scenario.type} color={typeColor[scenario.type] ?? '#94a3b8'} />
                              <ColorPill label={scenario.priority} color={priorityColor[scenario.priority] ?? '#64748b'} />
                            </span>
                          )}
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ol>
            )}
          </div>
        </div>
        <DialogFooter className="items-center gap-2 border-t border-border px-6 py-4">
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="button"
            disabled={environments.length === 0 || selectedScenarios.length === 0 || runMutation.isPending}
            onClick={() => runMutation.mutate()}>
            {runMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Play className="h-4 w-4 mr-1.5" />}
            Run {selectedScenarios.length} test{selectedScenarios.length === 1 ? '' : 's'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
