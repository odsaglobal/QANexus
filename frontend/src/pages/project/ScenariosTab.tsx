import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Sparkles, Edit2, Loader2, FileUp, RefreshCw, FolderPlus, Plus, Compass, CheckCircle2, XCircle, Wrench, MinusCircle, ChevronDown, ChevronRight, Play } from 'lucide-react';
import type { Scenario, StepRunStatus } from '../../api/types';
import { listRequirements, getRequirement } from '../../api/requirements';
import { generateScenarios, listScenarios, exploreScenario, runScenario, getLatestScenarioRun } from '../../api/scenarios';
import { listEnvironments } from '../../api/environments';
import { startExploration } from '../../api/explorer';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { ScenarioEditorDialog } from './ScenarioEditorDialog';
import { ImportScenariosDialog } from './ImportScenariosDialog';
import { TestRailSyncDialog } from './TestRailSyncDialog';
import { ManualFeatureDialog } from './ManualFeatureDialog';
import { ManualScenarioDialog } from './ManualScenarioDialog';
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

type SourceFilter = 'all' | 'AiGenerated' | 'Manual' | 'TestRail';

interface FeatureOption { id: string; label: string; }

export function ScenariosTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [featureId, setFeatureId] = useState('');
  const [sourceFilter, setSourceFilter] = useState<SourceFilter>('all');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [expandedScenarioId, setExpandedScenarioId] = useState<string | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [testRailOpen, setTestRailOpen] = useState(false);
  const [manualFeatureOpen, setManualFeatureOpen] = useState(false);
  const [manualScenarioOpen, setManualScenarioOpen] = useState(false);

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

  const featureOptions = useMemo<FeatureOption[]>(() => {
    const options: FeatureOption[] = [];
    for (const detail of detailQueries.data ?? []) {
      for (const module of detail.modules) {
        for (const feature of module.features) {
          options.push({ id: feature.id, label: `${module.name} › ${feature.name}` });
        }
      }
    }
    return options;
  }, [detailQueries.data]);

  const selectedFeatureLabel = featureOptions.find((f) => f.id === featureId)?.label ?? 'the selected feature';

  const { data: scenarios = [], isLoading: scenariosLoading } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const counts = useMemo(() => {
    const scoped = featureId ? scenarios.filter((s) => s.featureId === featureId) : scenarios;
    const bySource: Record<string, number> = { AiGenerated: 0, Manual: 0, TestRail: 0 };
    for (const scenario of scoped) {
      bySource[scenario.source] = (bySource[scenario.source] ?? 0) + 1;
    }
    return {
      total: scoped.length,
      AiGenerated: bySource.AiGenerated ?? 0,
      Manual: bySource.Manual ?? 0,
      TestRail: bySource.TestRail ?? 0,
    };
  }, [scenarios, featureId]);

  const visibleScenarios = useMemo(() => {
    let list = scenarios;
    if (featureId) {
      list = list.filter((s) => s.featureId === featureId);
    }
    if (sourceFilter !== 'all') {
      list = list.filter((s) => s.source === sourceFilter);
    }
    return list;
  }, [scenarios, featureId, sourceFilter]);

  const generateMutation = useMutation({
    mutationFn: () => generateScenarios(projectId, featureId),
    onSuccess: (created) => {
      setError(null);
      setNotice(`Generated ${created.length} AI scenario${created.length === 1 ? '' : 's'}.`);
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
    },
    onError: (e) => { setNotice(null); setError(getErrorMessage(e)); },
  });

  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
  });

  // The selected feature is explorable if it has manual/imported (non-AI) scenarios to walk.
  const featureHasDriveableScenarios = Boolean(featureId)
    && scenarios.some((s) => s.featureId === featureId && s.source !== 'AiGenerated');

  const exploreMutation = useMutation({
    mutationFn: () => {
      const env = environments.find((e) => e.isDefault) ?? environments[0];
      if (!env) {
        throw new Error('Add an environment with a Base URL first (Overview tab).');
      }
      return startExploration({ projectId, environmentId: env.id, featureId });
    },
    onSuccess: () => {
      setError(null);
      setNotice('Exploration started from this feature\u2019s scenarios. Opening Page Explorer\u2026');
      navigate(`/projects/${projectId}/explorer`);
    },
    onError: (e) => { setNotice(null); setError(getErrorMessage(e)); },
  });

  const hasFeatures = featureOptions.length > 0;
  const requiresFeature = hasFeatures && !featureId;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle>Scenario Management</CardTitle>
          <CardDescription>
            Generate scenarios with AI, import manual test cases (CSV/XLSX), or sync from TestRail — all stored with ordered steps.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}
          {notice && <Alert severity="success" className="text-sm">{notice}</Alert>}
          {!hasFeatures ? (
            <div className="space-y-3">
              <Alert severity="info" className="text-sm">
                Analyze a requirement document, or create a manual feature to author scenarios by hand.
              </Alert>
              <Button variant="outline" onClick={() => { setError(null); setNotice(null); setManualFeatureOpen(true); }}>
                <FolderPlus className="h-4 w-4 mr-1.5" /> New manual feature
              </Button>
            </div>
          ) : (
            <div className="flex flex-wrap items-end gap-3">
              <div className="space-y-1.5">
                <label className="text-xs font-medium text-muted-foreground">Feature</label>
                <Select value={featureId} onValueChange={(v) => { setFeatureId(v); setError(null); setNotice(null); }}>
                  <SelectTrigger className="w-80"><SelectValue placeholder="Select a feature…" /></SelectTrigger>
                  <SelectContent>
                    {featureOptions.map((f) => <SelectItem key={f.id} value={f.id}>{f.label}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <Button disabled={requiresFeature || generateMutation.isPending} onClick={() => generateMutation.mutate()}>
                {generateMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Sparkles className="h-4 w-4 mr-1.5" />}
                Generate with AI
              </Button>
              <Button variant="outline" disabled={requiresFeature} onClick={() => { setError(null); setNotice(null); setManualScenarioOpen(true); }}>
                <Plus className="h-4 w-4 mr-1.5" /> Add scenario
              </Button>
              <Button variant="outline" disabled={requiresFeature} onClick={() => { setError(null); setNotice(null); setImportOpen(true); }}>
                <FileUp className="h-4 w-4 mr-1.5" /> Import CSV/XLSX
              </Button>
              <Button variant="outline" disabled={requiresFeature} onClick={() => { setError(null); setNotice(null); setTestRailOpen(true); }}>
                <RefreshCw className="h-4 w-4 mr-1.5" /> Sync TestRail
              </Button>
              <Button
                variant="outline"
                disabled={!featureHasDriveableScenarios || exploreMutation.isPending}
                title={featureHasDriveableScenarios ? 'Run the explorer over this feature\u2019s manual/imported scenario steps' : 'Add a manual or imported scenario to this feature first'}
                onClick={() => { setError(null); setNotice(null); exploreMutation.mutate(); }}
              >
                {exploreMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Compass className="h-4 w-4 mr-1.5" />}
                Explore scenarios
              </Button>
              <Button variant="ghost" onClick={() => { setError(null); setNotice(null); setManualFeatureOpen(true); }}>
                <FolderPlus className="h-4 w-4 mr-1.5" /> New feature
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {counts.total > 0 && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <SummaryTile label="Total scenarios" value={counts.total} active={sourceFilter === 'all'} onClick={() => setSourceFilter('all')} />
          <SummaryTile label="AI generated" value={counts.AiGenerated ?? 0} active={sourceFilter === 'AiGenerated'} onClick={() => setSourceFilter('AiGenerated')} />
          <SummaryTile label="Manual" value={counts.Manual ?? 0} active={sourceFilter === 'Manual'} onClick={() => setSourceFilter('Manual')} />
          <SummaryTile label="TestRail" value={counts.TestRail ?? 0} active={sourceFilter === 'TestRail'} onClick={() => setSourceFilter('TestRail')} />
        </div>
      )}

      {featureId && (
        <div className="flex items-center gap-2 text-sm">
          <Badge variant="outline">Feature: {selectedFeatureLabel}</Badge>
          <Button variant="ghost" size="sm" className="h-7 text-violet-600" onClick={() => setFeatureId('')}>
            Show all features
          </Button>
        </div>
      )}

      <div className="space-y-3">
        {scenariosLoading && (
          <p className="text-muted-foreground text-sm flex items-center gap-2">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading scenarios…
          </p>
        )}
        {!scenariosLoading && counts.total === 0 && hasFeatures && (
          <p className="text-muted-foreground text-sm">
            No scenarios yet. Generate with AI, import a CSV/XLSX, or sync from TestRail.
          </p>
        )}
        {!scenariosLoading && counts.total > 0 && visibleScenarios.length === 0 && (
          <p className="text-muted-foreground text-sm">No scenarios match the selected source filter.</p>
        )}
        {visibleScenarios.map((scenario) => (
          <ScenarioCard
            key={scenario.id}
            scenario={scenario}
            projectId={projectId}
            isExpanded={expandedScenarioId === scenario.id}
            onToggle={() => setExpandedScenarioId((prev) => prev === scenario.id ? null : scenario.id)}
          />
        ))}
      </div>

      {importOpen && (
        <ImportScenariosDialog
          projectId={projectId}
          featureId={featureId}
          featureLabel={selectedFeatureLabel}
          open={importOpen}
          onClose={() => setImportOpen(false)}
          onImported={(count) => setNotice(`Imported ${count} manual scenario${count === 1 ? '' : 's'}.`)}
        />
      )}
      {testRailOpen && (
        <TestRailSyncDialog
          projectId={projectId}
          featureId={featureId}
          featureLabel={selectedFeatureLabel}
          open={testRailOpen}
          onClose={() => setTestRailOpen(false)}
          onSynced={(count) => setNotice(`Synced ${count} TestRail scenario${count === 1 ? '' : 's'}.`)}
        />
      )}
      {manualFeatureOpen && (
        <ManualFeatureDialog
          projectId={projectId}
          open={manualFeatureOpen}
          onClose={() => setManualFeatureOpen(false)}
          onCreated={(newFeatureId, label) => {
            setFeatureId(newFeatureId);
            setNotice(`Created feature "${label}". Now add or generate scenarios.`);
          }}
        />
      )}
      {manualScenarioOpen && (
        <ManualScenarioDialog
          projectId={projectId}
          featureId={featureId}
          featureLabel={selectedFeatureLabel}
          open={manualScenarioOpen}
          onClose={() => setManualScenarioOpen(false)}
          onCreated={() => setNotice('Manual scenario added.')}
        />
      )}
    </div>
  );
}

function SummaryTile({ label, value, active, onClick }: { label: string; value: number; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-lg border px-4 py-3 text-left transition-colors ${active ? 'border-violet-300 bg-violet-50' : 'border-border hover:bg-muted'}`}
    >
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-2xl font-bold text-foreground mt-0.5">{value}</div>
    </button>
  );
}

function ScenarioCard({ scenario, projectId, isExpanded, onToggle }: { scenario: Scenario; projectId: string; isExpanded: boolean; onToggle: () => void }) {
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
    mutationFn: () => exploreScenario(projectId, scenario.id),
    onSuccess: (session) => {
      setRunError(null);
      setLiveMode('explore');
      setLiveSessionId(session.id);
      queryClient.invalidateQueries({ queryKey: ['scenario-run', projectId, scenario.id] });
    },
    onError: (e) => setRunError(getErrorMessage(e)),
  });

  const runMutation = useMutation({
    mutationFn: () => runScenario(projectId, scenario.id),
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
    <>
      <Card>
        <CardContent className="p-4">
          <div
            role="button"
            tabIndex={0}
            className="flex items-center gap-2 mb-2 w-full text-left cursor-pointer"
            onClick={onToggle}
            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onToggle(); } }}
            aria-expanded={isExpanded}
          >
            {isExpanded
              ? <ChevronDown className="h-4 w-4 flex-shrink-0 text-muted-foreground" />
              : <ChevronRight className="h-4 w-4 flex-shrink-0 text-muted-foreground" />}
            <span className="h-2 w-2 rounded-full flex-shrink-0"
              style={{ background: typeColor[scenario.type] ?? '#94a3b8' }} />
            <span className="text-sm font-semibold flex-1 text-foreground">{scenario.title}</span>
            <Button
              variant="outline"
              size="sm"
              className="h-7"
              disabled={busy}
              onClick={(e) => { e.stopPropagation(); exploreMutation.mutate(); }}
              title="Explore the app to discover & ground this scenario's real steps"
            >
              {running || exploreMutation.isPending
                ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                : <Compass className="h-3.5 w-3.5 mr-1" />}
              {running ? 'Exploring…' : run ? 'Re-explore' : 'Explore'}
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-7"
              disabled={busy || scenario.steps.length === 0}
              onClick={(e) => { e.stopPropagation(); runMutation.mutate(); }}
              title={scenario.steps.length === 0 ? 'Add or ground steps first (Explore), then Run' : "Run this scenario's saved steps as a test"}
            >
              {runMutation.isPending
                ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                : <Play className="h-3.5 w-3.5 mr-1" />}
              Run
            </Button>
            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); setEditorOpen(true); }}>
              <Edit2 className="h-3.5 w-3.5" />
            </Button>
          </div>

          <div className="flex flex-wrap items-center gap-2 mb-2 pl-6">
            <Badge variant={source.variant}>{source.label}</Badge>
            <Badge>{scenario.type}</Badge>
            <Badge variant="outline">Priority: {scenario.priority}</Badge>
            <Badge variant="outline">Risk: {scenario.risk}</Badge>
          </div>

          {isExpanded && (
          <>
          {runError && <Alert severity="error" className="text-xs mb-2">{runError}</Alert>}

          {scenario.preconditions && (
            <p className="text-xs text-muted-foreground mb-2">
              <strong>Preconditions:</strong> {scenario.preconditions}
            </p>
          )}

          <ol className="space-y-1 pl-1">
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

          {run && (
            <div className="flex items-center gap-2 mt-3 pt-2 border-t border-border">
              <Badge variant={outcomeVariant as 'success' | 'warning' | 'destructive' | 'secondary'}>{run.outcome}</Badge>
              <span className="text-xs text-muted-foreground">
                {run.passedSteps} passed · {run.healedSteps} healed · {run.failedSteps} failed
              </span>
            </div>
          )}

          {scenario.expectedResult && (
            <p className="text-sm mt-2"><strong>Expected:</strong> {scenario.expectedResult}</p>
          )}

          {scenario.tags.length > 0 && (
            <div className="flex flex-wrap gap-1 mt-2">
              {scenario.tags.map((tag) => <Badge key={tag} variant="outline" className="text-xs">{tag}</Badge>)}
            </div>
          )}
          </>
          )}
        </CardContent>
      </Card>

      {editorOpen && (
        <ScenarioEditorDialog scenario={scenario} projectId={projectId} open={editorOpen} onClose={() => setEditorOpen(false)} />
      )}

      {liveSessionId && (
        <LiveExplorationDialog
          sessionId={liveSessionId}
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
    </>
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
