import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Bot, FileText, FlaskConical, GitBranch, Sparkles, TriangleAlert } from 'lucide-react';
import { listProjects } from '../api/projects';
import { listRequirements } from '../api/requirements';
import { getKnowledgeGraph, listScenarios } from '../api/scenarios';
import { getStoredProjectId, setStoredProjectId } from '../lib/projectSelection';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';

export function AiIntelligencePage() {
  const [selectedProjectId, setSelectedProjectId] = useState<string>(() => {
    return getStoredProjectId();
  });

  const projectsQuery = useQuery({
    queryKey: ['projects', { page: 1, pageSize: 100 }],
    queryFn: () => listProjects({ page: 1, pageSize: 100 }),
  });

  const projects = projectsQuery.data?.items ?? [];
  const activeProject = projects.find((project) => project.id === selectedProjectId) ?? projects[0];
  const activeProjectId = activeProject?.id ?? '';

  useEffect(() => {
    if (!activeProjectId) return;
    if (selectedProjectId === activeProjectId) return;
    setSelectedProjectId(activeProjectId);
  }, [activeProjectId, selectedProjectId]);

  useEffect(() => {
    if (!selectedProjectId) return;
    setStoredProjectId(selectedProjectId);
  }, [selectedProjectId]);

  const requirementsQuery = useQuery({
    queryKey: ['requirements', activeProjectId],
    queryFn: () => listRequirements(activeProjectId),
    enabled: Boolean(activeProjectId),
  });

  const scenariosQuery = useQuery({
    queryKey: ['scenarios', activeProjectId],
    queryFn: () => listScenarios(activeProjectId),
    enabled: Boolean(activeProjectId),
  });

  const graphQuery = useQuery({
    queryKey: ['knowledge-graph', activeProjectId],
    queryFn: () => getKnowledgeGraph(activeProjectId),
    enabled: Boolean(activeProjectId),
  });

  const requirements = requirementsQuery.data ?? [];
  const scenarios = scenariosQuery.data ?? [];
  const graph = graphQuery.data;

  const summary = useMemo(() => {
    const analyzed = requirements.filter((req) => req.status === 'Analyzed').length;
    const failed = requirements.filter((req) => req.status === 'Failed').length;
    const modules = requirements.reduce((sum, req) => sum + req.moduleCount, 0);
    const features = requirements.reduce((sum, req) => sum + req.featureCount, 0);

    const byType = scenarios.reduce<Record<string, number>>((acc, scenario) => {
      const key = scenario.type || 'Unknown';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

    const prioritizedTypes = Object.entries(byType)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 4);

    const graphNodes = graph?.nodes.length ?? 0;
    const graphEdges = graph?.edges.length ?? 0;

    const requirementReadiness = requirements.length === 0
      ? 0
      : Math.round((analyzed / requirements.length) * 100);
    const scenarioReadiness = features === 0
      ? 0
      : Math.min(100, Math.round((scenarios.length / features) * 100));
    const graphReadiness = graphNodes === 0 ? 0 : Math.min(100, Math.round((graphEdges / graphNodes) * 100));
    const score = Math.round((requirementReadiness + scenarioReadiness + graphReadiness) / 3);

    return {
      analyzed,
      failed,
      modules,
      features,
      prioritizedTypes,
      graphNodes,
      graphEdges,
      score,
      requirementReadiness,
      scenarioReadiness,
      graphReadiness,
    };
  }, [requirements, scenarios, graph]);

  const isLoading = projectsQuery.isLoading || requirementsQuery.isLoading || scenariosQuery.isLoading || graphQuery.isLoading;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-foreground">AI Intelligence</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Unified view of requirement analysis, scenario coverage and graph maturity.
          </p>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {projects.map((project) => (
            <button
              key={project.id}
              type="button"
              onClick={() => setSelectedProjectId(project.id)}
              className={`h-9 px-3 rounded-md text-sm border transition-colors ${project.id === activeProjectId ? 'bg-violet-50 border-violet-200 text-violet-700' : 'border-input text-foreground hover:bg-muted'}`}
            >
              {project.name}
            </button>
          ))}
        </div>
      </div>

      {!activeProjectId && (
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            Create a project first to start AI-driven requirement and scenario intelligence.
          </CardContent>
        </Card>
      )}

      {activeProjectId && (
        <>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <Card>
              <CardContent className="pt-6">
                <div className="text-sm text-muted-foreground">Intelligence Score</div>
                <div className="text-3xl font-bold mt-1">{summary.score}%</div>
                <p className="text-xs text-muted-foreground mt-2">Composite of requirement, scenario and graph readiness.</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="pt-6">
                <div className="text-sm text-muted-foreground">Requirements analyzed</div>
                <div className="text-3xl font-bold mt-1">{summary.analyzed}/{requirements.length}</div>
                <p className="text-xs text-muted-foreground mt-2">{summary.modules} modules • {summary.features} features extracted</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="pt-6">
                <div className="text-sm text-muted-foreground">Scenario inventory</div>
                <div className="text-3xl font-bold mt-1">{scenarios.length}</div>
                <p className="text-xs text-muted-foreground mt-2">Generated from AI feature understanding</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="pt-6">
                <div className="text-sm text-muted-foreground">Knowledge graph</div>
                <div className="text-3xl font-bold mt-1">{summary.graphNodes}</div>
                <p className="text-xs text-muted-foreground mt-2">{summary.graphEdges} connected edges</p>
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle className="text-base">Pipeline readiness</CardTitle>
                <CardDescription>Progress across AI ingestion, generation and linkage.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <ReadinessRow label="Requirement extraction" value={summary.requirementReadiness} icon={<FileText className="h-4 w-4" />} />
                <ReadinessRow label="Scenario generation density" value={summary.scenarioReadiness} icon={<FlaskConical className="h-4 w-4" />} />
                <ReadinessRow label="Knowledge graph connectedness" value={summary.graphReadiness} icon={<GitBranch className="h-4 w-4" />} />
                {summary.failed > 0 && (
                  <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900 flex items-center gap-2">
                    <TriangleAlert className="h-4 w-4" />
                    {summary.failed} requirement{summary.failed > 1 ? 's are' : ' is'} in Failed status and should be re-analyzed.
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Top scenario types</CardTitle>
                <CardDescription>Most generated scenario categories.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                {summary.prioritizedTypes.length === 0 && (
                  <p className="text-sm text-muted-foreground">No scenarios generated yet.</p>
                )}
                {summary.prioritizedTypes.map(([type, count]) => (
                  <div key={type} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                    <span className="font-medium text-foreground">{type}</span>
                    <Badge variant="outline">{count}</Badge>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Card>
              <CardHeader>
                <CardTitle className="text-base flex items-center gap-2"><FileText className="h-4 w-4 text-violet-600" /> Requirement Intelligence</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="text-sm text-muted-foreground">Upload and analyze product documents to extract modules, features and stories.</p>
                <Button asChild size="sm" className="w-full">
                  <RouterLink to={`/projects/${activeProjectId}/requirements`}>Open Requirements</RouterLink>
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base flex items-center gap-2"><FlaskConical className="h-4 w-4 text-violet-600" /> Scenario Engine</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="text-sm text-muted-foreground">Generate positive, negative, boundary and security scenarios from feature context.</p>
                <Button asChild size="sm" className="w-full">
                  <RouterLink to={`/projects/${activeProjectId}/scenarios`}>Open Scenarios</RouterLink>
                </Button>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base flex items-center gap-2"><Bot className="h-4 w-4 text-violet-600" /> Knowledge Synthesis</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <p className="text-sm text-muted-foreground">Inspect graph connectivity between projects, modules, features and scenarios.</p>
                <Button asChild size="sm" className="w-full">
                  <RouterLink to={`/projects/${activeProjectId}/knowledge-graph`}>Open Graph</RouterLink>
                </Button>
              </CardContent>
            </Card>
          </div>

          <div className="rounded-lg border border-violet-200 bg-violet-50 p-4 flex items-start gap-3">
            <Sparkles className="h-5 w-5 text-violet-600 mt-0.5" />
            <div>
              <h3 className="font-semibold text-violet-900">AI recommendation</h3>
              <p className="text-sm text-violet-900/90 mt-1">
                {summary.requirementReadiness < 100
                  ? 'Finish requirement analysis for all uploaded documents to improve generation quality.'
                  : summary.scenarioReadiness < 80
                    ? 'Generate more scenarios for uncovered features to improve execution confidence.'
                    : 'Pipeline health is strong. Next step: run executions and feed failures back into intelligence recommendations.'}
              </p>
            </div>
          </div>

          {isLoading && (
            <p className="text-sm text-muted-foreground">Refreshing AI intelligence metrics...</p>
          )}
        </>
      )}
    </div>
  );
}

function ReadinessRow({ label, value, icon }: { label: string; value: number; icon: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-sm">
        <span className="inline-flex items-center gap-2 text-foreground">{icon}{label}</span>
        <span className="font-semibold">{value}%</span>
      </div>
      <div className="h-2 rounded-full bg-muted overflow-hidden">
        <div className="h-full bg-violet-500" style={{ width: `${value}%` }} />
      </div>
    </div>
  );
}