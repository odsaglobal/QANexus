import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueries } from '@tanstack/react-query';
import {
  FileText, Layers, FlaskConical, Server, Compass, Sparkles,
  ArrowRight, CheckCircle2, Circle, AlertTriangle, Clock, ExternalLink,
} from 'lucide-react';
import { PieChart, Pie, Cell, ResponsiveContainer } from 'recharts';
import { listRequirements } from '../../api/requirements';
import { listScenarios } from '../../api/scenarios';
import { listEnvironments } from '../../api/environments';
import { listExplorationSessions } from '../../api/explorer';
import { getErrorMessage } from '../../lib/apiClient';
import { tabToPathSegment, type ProjectTab } from '../../lib/projectTabs';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Alert } from '../../components/ui/alert';

const scenarioTypeColor: Record<string, string> = {
  Positive: '#22c55e', Negative: '#ef4444', Boundary: '#f59e0b', Smoke: '#a78bfa',
  Security: '#ec4899', Regression: '#14b8a6', Accessibility: '#8b5cf6', Api: '#0ea5e9',
};

const explorationStatusVariant: Record<string, 'info' | 'warning' | 'success' | 'destructive' | 'secondary'> = {
  Pending: 'info', Running: 'warning', Completed: 'success', Failed: 'destructive', Cancelled: 'secondary',
};

export function OverviewTab({ projectId }: { projectId: string }) {
  const navigate = useNavigate();

  const results = useQueries({
    queries: [
      { queryKey: ['requirements', projectId], queryFn: () => listRequirements(projectId) },
      { queryKey: ['scenarios', projectId], queryFn: () => listScenarios(projectId) },
      { queryKey: ['environments', projectId], queryFn: () => listEnvironments(projectId) },
      { queryKey: ['exploration-sessions', projectId], queryFn: () => listExplorationSessions(projectId) },
    ],
  });

  const [requirementsQ, scenariosQ, environmentsQ, sessionsQ] = results;
  const requirements = requirementsQ.data ?? [];
  const scenarios = scenariosQ.data ?? [];
  const environments = environmentsQ.data ?? [];
  const sessions = sessionsQ.data ?? [];

  const isLoading = results.some((r) => r.isLoading);
  const errored = results.find((r) => r.isError);

  const goTo = (tab: ProjectTab) => navigate(`/projects/${projectId}/${tabToPathSegment(tab)}`);

  const stats = useMemo(() => {
    const analyzed = requirements.filter((r) => r.status === 'Analyzed').length;
    const featureCount = requirements.reduce((sum, r) => sum + r.featureCount, 0);
    const bySource = { AiGenerated: 0, Manual: 0, TestRail: 0 } as Record<string, number>;
    const byType: Record<string, number> = {};
    for (const s of scenarios) {
      bySource[s.source] = (bySource[s.source] ?? 0) + 1;
      byType[s.type] = (byType[s.type] ?? 0) + 1;
    }
    const discoveredPages = sessions.reduce((sum, s) => sum + s.pagesDiscovered, 0);
    const discoveredElements = sessions.reduce((sum, s) => sum + s.elementsDiscovered, 0);
    const activeExplorations = sessions.filter((s) => s.status === 'Running' || s.status === 'Pending').length;
    return {
      requirementCount: requirements.length,
      analyzed,
      featureCount,
      scenarioCount: scenarios.length,
      bySource,
      byType,
      environmentCount: environments.length,
      hasDefaultEnv: environments.some((e) => e.isDefault),
      sessionCount: sessions.length,
      discoveredPages,
      discoveredElements,
      activeExplorations,
    };
  }, [requirements, scenarios, environments, sessions]);

  const typeChartData = useMemo(
    () => Object.entries(stats.byType)
      .map(([name, value]) => ({ name, value, color: scenarioTypeColor[name] ?? '#94a3b8' }))
      .sort((a, b) => b.value - a.value),
    [stats.byType],
  );

  const analysisPct = stats.requirementCount > 0
    ? Math.round((stats.analyzed / stats.requirementCount) * 100)
    : 0;

  const recentSessions = useMemo(
    () => [...sessions]
      .sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime())
      .slice(0, 5),
    [sessions],
  );

  const checklist = [
    { done: stats.environmentCount > 0, label: 'Add an environment', tab: 'environments' as ProjectTab, hint: 'Point the platform at your app URL.' },
    { done: stats.requirementCount > 0, label: 'Upload a requirement document', tab: 'requirements' as ProjectTab, hint: 'AI extracts modules, features and user stories.' },
    { done: stats.analyzed > 0, label: 'Analyze requirements', tab: 'requirements' as ProjectTab, hint: 'Turn documents into a structured feature tree.' },
    { done: stats.scenarioCount > 0, label: 'Generate test scenarios', tab: 'scenarios' as ProjectTab, hint: 'Author manually or generate with AI.' },
    { done: stats.sessionCount > 0, label: 'Run an application exploration', tab: 'explorer' as ProjectTab, hint: 'Crawl the app to discover pages and locators.' },
  ];
  const completedSteps = checklist.filter((c) => c.done).length;

  if (errored) {
    return (
      <Alert severity="error" className="text-sm">
        Failed to load project overview: {getErrorMessage(errored.error)}
      </Alert>
    );
  }

  return (
    <div className="space-y-5">
      {/* KPI stat cards */}
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
        <StatCard
          label="Requirements" value={stats.requirementCount}
          sublabel={`${stats.analyzed} analyzed`} icon={<FileText className="h-5 w-5" />}
          iconBg="bg-blue-50 text-blue-600" loading={isLoading} onClick={() => goTo('requirements')}
        />
        <StatCard
          label="Features" value={stats.featureCount}
          sublabel="from documents" icon={<Layers className="h-5 w-5" />}
          iconBg="bg-emerald-50 text-emerald-600" loading={isLoading} onClick={() => goTo('requirements')}
        />
        <StatCard
          label="Scenarios" value={stats.scenarioCount}
          sublabel={`${stats.bySource.AiGenerated} AI · ${stats.bySource.Manual} manual`}
          icon={<FlaskConical className="h-5 w-5" />}
          iconBg="bg-violet-50 text-violet-600" loading={isLoading} onClick={() => goTo('scenarios')}
        />
        <StatCard
          label="Environments" value={stats.environmentCount}
          sublabel={stats.hasDefaultEnv ? 'default set' : 'no default'} icon={<Server className="h-5 w-5" />}
          iconBg="bg-amber-50 text-amber-600" loading={isLoading} onClick={() => goTo('environments')}
        />
        <StatCard
          label="Pages discovered" value={stats.discoveredPages}
          sublabel={`${stats.discoveredElements} elements`} icon={<Compass className="h-5 w-5" />}
          iconBg="bg-pink-50 text-pink-600" loading={isLoading} onClick={() => goTo('explorer')}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        {/* Getting started / checklist */}
        <Card className="lg:col-span-2">
          <CardHeader className="pb-3">
            <div className="flex items-start justify-between">
              <div>
                <CardTitle>Getting started</CardTitle>
                <CardDescription>
                  {completedSteps === checklist.length
                    ? 'All set — your project is fully configured.'
                    : `${completedSteps} of ${checklist.length} steps complete.`}
                </CardDescription>
              </div>
              <Badge variant={completedSteps === checklist.length ? 'success' : 'secondary'}>
                {Math.round((completedSteps / checklist.length) * 100)}%
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-1">
            {checklist.map((item) => (
              <button
                key={item.label}
                type="button"
                onClick={() => goTo(item.tab)}
                className="group flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left transition-colors hover:bg-muted focus:outline-none focus-visible:ring-2 focus-visible:ring-violet-500"
              >
                {item.done
                  ? <CheckCircle2 className="h-5 w-5 flex-shrink-0 text-green-600" />
                  : <Circle className="h-5 w-5 flex-shrink-0 text-muted-foreground" />}
                <span className="flex-1">
                  <span className={`block text-sm font-medium ${item.done ? 'text-muted-foreground line-through' : 'text-foreground'}`}>
                    {item.label}
                  </span>
                  <span className="block text-xs text-muted-foreground">{item.hint}</span>
                </span>
                <ArrowRight className="h-4 w-4 flex-shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100" />
              </button>
            ))}
          </CardContent>
        </Card>

        {/* Requirement analysis progress + scenario composition */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle>Analysis coverage</CardTitle>
            <CardDescription>Requirement documents processed by AI.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="flex items-baseline justify-between mb-1.5">
                <span className="text-2xl font-bold text-foreground">{analysisPct}%</span>
                <span className="text-xs text-muted-foreground">{stats.analyzed}/{stats.requirementCount} analyzed</span>
              </div>
              <div
                className="h-2 w-full overflow-hidden rounded-full bg-muted"
                role="progressbar"
                aria-valuenow={analysisPct}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label="Requirement analysis progress"
              >
                <div className="h-full rounded-full bg-violet-500 transition-all" style={{ width: `${analysisPct}%` }} />
              </div>
            </div>

            <div className="pt-1">
              <p className="text-xs font-semibold text-muted-foreground mb-2">Scenario mix</p>
              {typeChartData.length === 0 ? (
                <p className="text-xs text-muted-foreground">No scenarios yet.</p>
              ) : (
                <div className="flex items-center gap-3">
                  <div className="h-24 w-24 flex-shrink-0">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie data={typeChartData} dataKey="value" innerRadius={26} outerRadius={44} paddingAngle={2}>
                          {typeChartData.map((entry) => <Cell key={entry.name} fill={entry.color} />)}
                        </Pie>
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                  <ul className="flex-1 space-y-1">
                    {typeChartData.slice(0, 5).map((entry) => (
                      <li key={entry.name} className="flex items-center gap-2 text-xs">
                        <span className="h-2 w-2 rounded-full" style={{ background: entry.color }} />
                        <span className="flex-1 text-muted-foreground">{entry.name}</span>
                        <span className="font-medium text-foreground">{entry.value}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Recent explorations */}
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Recent explorations</CardTitle>
              <CardDescription>
                {stats.activeExplorations > 0
                  ? `${stats.activeExplorations} running now`
                  : 'AI-guided crawls of your application.'}
              </CardDescription>
            </div>
            <Button variant="outline" size="sm" onClick={() => goTo('explorer')}>
              <Compass className="h-4 w-4 mr-1.5" /> Open explorer
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {recentSessions.length === 0 ? (
            <div className="flex flex-col items-center justify-center gap-2 py-8 text-center">
              <Compass className="h-8 w-8 text-muted-foreground/50" />
              <p className="text-sm text-muted-foreground">No explorations yet.</p>
              <Button variant="ghost" size="sm" className="text-violet-600" onClick={() => goTo('explorer')}>
                Start your first exploration <ArrowRight className="h-3.5 w-3.5 ml-1" />
              </Button>
            </div>
          ) : (
            <ul className="divide-y divide-border">
              {recentSessions.map((session) => (
                <li key={session.id} className="flex items-center gap-3 py-2.5">
                  <Badge variant={explorationStatusVariant[session.status] ?? 'secondary'}>{session.status}</Badge>
                  <span className="flex-1 text-sm text-foreground truncate">
                    {session.pagesDiscovered} pages · {session.elementsDiscovered} elements
                  </span>
                  <span className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Clock className="h-3 w-3" />
                    {new Date(session.createdAtUtc).toLocaleString()}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {/* Quick actions */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <QuickAction
          icon={<FileText className="h-5 w-5" />} title="Upload requirements"
          description="Extract features with AI" onClick={() => goTo('requirements')}
        />
        <QuickAction
          icon={<Sparkles className="h-5 w-5" />} title="Generate scenarios"
          description="AI, manual or TestRail" onClick={() => goTo('scenarios')}
        />
        <QuickAction
          icon={<ExternalLink className="h-5 w-5" />} title="Explore application"
          description="Discover pages & locators" onClick={() => goTo('explorer')}
        />
      </div>

      {!isLoading && stats.environmentCount === 0 && (
        <Alert severity="warning" className="text-sm">
          <span className="inline-flex items-center gap-1.5">
            <AlertTriangle className="h-4 w-4" />
            Add an environment with your application URL to unlock exploration and scenario execution.
          </span>
        </Alert>
      )}
    </div>
  );
}

function StatCard({ label, value, sublabel, icon, iconBg, loading, onClick }: {
  label: string; value: number; sublabel?: string; icon: React.ReactNode;
  iconBg: string; loading?: boolean; onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="rounded-xl border border-border bg-card p-4 text-left transition-colors hover:border-violet-200 hover:bg-muted/40 focus:outline-none focus-visible:ring-2 focus-visible:ring-violet-500"
    >
      <div className="flex items-start justify-between">
        <div className="min-w-0">
          <p className="text-xs text-muted-foreground">{label}</p>
          {loading
            ? <div className="mt-1.5 h-7 w-12 animate-pulse rounded bg-muted" />
            : <p className="text-2xl font-bold text-foreground">{value}</p>}
          {sublabel && <p className="mt-0.5 truncate text-xs text-muted-foreground">{sublabel}</p>}
        </div>
        <div className={`flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg ${iconBg}`}>
          {icon}
        </div>
      </div>
    </button>
  );
}

function QuickAction({ icon, title, description, onClick }: {
  icon: React.ReactNode; title: string; description: string; onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="group flex items-center gap-3 rounded-xl border border-border bg-card p-4 text-left transition-colors hover:border-violet-200 hover:bg-muted/40 focus:outline-none focus-visible:ring-2 focus-visible:ring-violet-500"
    >
      <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg bg-violet-50 text-violet-600">
        {icon}
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-foreground">{title}</p>
        <p className="truncate text-xs text-muted-foreground">{description}</p>
      </div>
      <ArrowRight className="h-4 w-4 flex-shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
    </button>
  );
}
