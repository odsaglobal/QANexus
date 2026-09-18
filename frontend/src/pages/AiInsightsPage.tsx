import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ChevronRight,
  Info,
  Lightbulb,
  Loader2,
  RefreshCw,
  Sparkles,
  Target,
  Wrench,
} from 'lucide-react';
import { getAiInsights } from '../api/insights';
import { listProjects } from '../api/projects';
import { Alert } from '../components/ui/alert';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent } from '../components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import type { AiInsight, InsightCategory, InsightSeverity } from '../api/types';

const ALL = '__all__';

const CATEGORIES: { key: InsightCategory | 'All'; label: string }[] = [
  { key: 'All', label: 'All' },
  { key: 'Reliability', label: 'Reliability' },
  { key: 'Coverage', label: 'Coverage' },
  { key: 'Quality', label: 'Quality' },
  { key: 'Maintenance', label: 'Maintenance' },
];

function CategoryIcon({ category }: { category: InsightCategory }) {
  const cls = 'h-4 w-4 shrink-0';
  if (category === 'Reliability') return <AlertTriangle className={`${cls} text-amber-500`} />;
  if (category === 'Coverage') return <Target className={`${cls} text-sky-500`} />;
  if (category === 'Maintenance') return <Wrench className={`${cls} text-violet-500`} />;
  return <Lightbulb className={`${cls} text-blue-500`} />;
}

function severityVariant(severity: InsightSeverity): 'info' | 'warning' | 'destructive' {
  if (severity === 'Critical') return 'destructive';
  if (severity === 'Warning') return 'warning';
  return 'info';
}

function relativeTime(iso?: string | null): string | null {
  if (!iso) return null;
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000);
  if (days <= 0) return 'today';
  if (days === 1) return 'yesterday';
  if (days < 30) return `${days} days ago`;
  return `${Math.floor(days / 30)} month(s) ago`;
}

export function AiInsightsPage() {
  const [selectedProjectId, setSelectedProjectId] = useState<string>(ALL);
  const [category, setCategory] = useState<InsightCategory | 'All'>('All');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const { data: projectPage } = useQuery({
    queryKey: ['projects'],
    queryFn: () => listProjects({ page: 1, pageSize: 100 }),
  });
  const projects = projectPage?.items ?? [];

  const projectId = selectedProjectId === ALL ? undefined : selectedProjectId;

  const insightsQuery = useQuery({
    queryKey: ['ai-insights', projectId ?? ALL],
    queryFn: () => getAiInsights(projectId),
  });

  const all = useMemo(() => insightsQuery.data?.insights ?? [], [insightsQuery.data]);
  const filtered = useMemo(
    () => (category === 'All' ? all : all.filter((i) => i.category === category)),
    [all, category],
  );

  const critical = all.filter((i) => i.severity === 'Critical').length;
  const warning = all.filter((i) => i.severity === 'Warning').length;

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-foreground">AI Insights</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Findings derived from your execution history and authoring state — each one shows the evidence behind it.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Select value={selectedProjectId} onValueChange={setSelectedProjectId}>
            <SelectTrigger className="w-[220px]">
              <SelectValue placeholder="All projects" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All projects</SelectItem>
              {projects.map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {p.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="icon"
            aria-label="Refresh insights"
            onClick={() => insightsQuery.refetch()}
          >
            <RefreshCw className={insightsQuery.isFetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <Stat label="Open findings" value={all.length} />
        <Stat label="Critical" value={critical} tone={critical > 0 ? 'text-red-600' : undefined} />
        <Stat label="Warnings" value={warning} tone={warning > 0 ? 'text-amber-600' : undefined} />
        <Stat
          label="Analyzed"
          value={insightsQuery.data?.runsAnalyzed ?? 0}
          hint={`${insightsQuery.data?.stepResultsAnalyzed ?? 0} step results across ${
            insightsQuery.data?.scenariosAnalyzed ?? 0
          } scenarios`}
        />
      </div>

      <div className="flex items-center gap-2">
        {CATEGORIES.map((c) => {
          const count = c.key === 'All' ? all.length : all.filter((i) => i.category === c.key).length;
          const active = category === c.key;
          return (
            <button
              key={c.key}
              onClick={() => setCategory(c.key)}
              className={`h-9 rounded-md border px-3 text-sm transition ${
                active
                  ? 'border-violet-200 bg-violet-50 text-violet-700'
                  : 'border-input hover:bg-accent'
              }`}
            >
              {c.label}
              <span className="ml-1.5 text-xs text-muted-foreground">{count}</span>
            </button>
          );
        })}
      </div>

      {insightsQuery.isLoading ? (
        <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" /> Analyzing execution history…
        </p>
      ) : insightsQuery.isError ? (
        <Alert severity="error" className="text-sm">
          Could not load insights. Try refreshing.
        </Alert>
      ) : all.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-16 text-center">
            <Sparkles className="h-8 w-8 text-violet-400" />
            <p className="text-sm font-medium">Nothing to flag</p>
            <p className="max-w-md text-sm text-muted-foreground">
              No flaky steps, coverage gaps or authoring issues were detected in the data available. Findings
              appear here as scenarios accumulate run history.
            </p>
          </CardContent>
        </Card>
      ) : filtered.length === 0 ? (
        <Alert severity="info" className="text-sm">
          No {category.toLowerCase()} findings. Switch category to see the other {all.length} finding(s).
        </Alert>
      ) : (
        <div className="space-y-3">
          {filtered.map((insight) => (
            <InsightCard
              key={insight.id}
              insight={insight}
              open={expanded.has(insight.id)}
              onToggle={() => toggle(insight.id)}
              showProject={selectedProjectId === ALL}
            />
          ))}
        </div>
      )}

      {insightsQuery.data && (
        <p className="text-xs text-muted-foreground">
          Generated {new Date(insightsQuery.data.generatedAtUtc).toLocaleString()} from{' '}
          {insightsQuery.data.stepResultsAnalyzed} step result(s) across {insightsQuery.data.runsAnalyzed} run(s).
        </p>
      )}
    </div>
  );
}

function InsightCard({
  insight,
  open,
  onToggle,
  showProject,
}: {
  insight: AiInsight;
  open: boolean;
  onToggle: () => void;
  showProject: boolean;
}) {
  const seen = relativeTime(insight.lastSeenUtc);

  return (
    <Card>
      <CardContent className="space-y-2.5 pt-5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-start gap-2">
            <CategoryIcon category={insight.category} />
            <div className="min-w-0">
              <p className="text-base font-medium leading-snug">{insight.title}</p>
              <p className="mt-0.5 text-xs text-muted-foreground">
                {showProject && insight.projectName ? `${insight.projectName} · ` : ''}
                {insight.category}
                {seen ? ` · last seen ${seen}` : ''}
              </p>
            </div>
          </div>
          <Badge variant={severityVariant(insight.severity)} className="shrink-0">
            {insight.severity}
          </Badge>
        </div>

        <p className="text-sm text-muted-foreground">{insight.summary}</p>

        <div className="flex items-start gap-2 rounded-md border border-violet-100 bg-violet-50/60 px-3 py-2 text-violet-900">
          <Sparkles className="mt-0.5 h-4 w-4 shrink-0 text-violet-600" />
          <p className="text-sm">
            <span className="font-medium">Suggested action:</span> {insight.suggestedAction}
          </p>
        </div>

        {insight.evidence.length > 0 && (
          <div>
            <button
              onClick={onToggle}
              className="flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground"
            >
              <ChevronRight className={`h-3.5 w-3.5 transition-transform ${open ? 'rotate-90' : ''}`} />
              {open ? 'Hide' : 'Show'} evidence ({insight.evidence.length})
            </button>
            {open && (
              <ul className="mt-2 space-y-1 border-l-2 border-border pl-3">
                {insight.evidence.map((line, i) => (
                  <li key={i} className="font-mono text-[11px] leading-relaxed text-muted-foreground">
                    {line}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function Stat({ label, value, tone, hint }: { label: string; value: number; tone?: string; hint?: string }) {
  return (
    <Card>
      <CardContent className="pt-6">
        <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
          {label}
          {hint && <Info className="h-3 w-3" aria-label={hint} />}
        </div>
        <div className={`mt-1 text-3xl font-bold ${tone ?? ''}`}>{value}</div>
        {hint && <p className="mt-1 text-[11px] text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  );
}
