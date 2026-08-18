import { useCallback, useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Background, Controls, Position, ReactFlow, type Edge, type Node,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import type { KnowledgeGraph, Scenario } from '../../api/types';
import { getKnowledgeGraph, listScenarios } from '../../api/scenarios';
import { Alert } from '../../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';

const typeStyles: Record<string, { bg: string; border: string; text: string }> = {
  project:  { bg: '#f5f3ff', border: '#7c3aed', text: '#5b21b6' },
  module:   { bg: '#f0fdf4', border: '#16a34a', text: '#15803d' },
  feature:  { bg: '#eff6ff', border: '#2563eb', text: '#1d4ed8' },
  scenario: { bg: '#fff7ed', border: '#ea580c', text: '#c2410c' },
  step:     { bg: '#ecfeff', border: '#0891b2', text: '#0e7490' },
};

const COLUMN: Record<string, number> = { project: 0, module: 1, feature: 2, scenario: 3, step: 4 };

function scenarioGuid(nodeId: string): string {
  return nodeId.includes(':') ? nodeId.slice(nodeId.indexOf(':') + 1) : nodeId;
}

/**
 * Builds the visible nodes/edges for the current expansion state (progressive drill-down):
 * project → modules → features → scenarios, and a scenario expands into its STEPS rendered as a
 * connected path (scenario → step 1 → step 2 → …). Only children of an expanded node are shown.
 */
function buildGraph(
  graph: KnowledgeGraph,
  expanded: Set<string>,
  scenarioById: Map<string, Scenario>,
): { nodes: Node[]; edges: Edge[] } {
  const nodeById = new Map(graph.nodes.map((n) => [n.id, n]));
  const childrenOf = new Map<string, string[]>();
  const hasParent = new Set<string>();

  for (const e of graph.edges) {
    const arr = childrenOf.get(e.source) ?? [];
    arr.push(e.target);
    childrenOf.set(e.source, arr);
    hasParent.add(e.target);
  }

  const roots = graph.nodes.filter((n) => !hasParent.has(n.id));
  const visible = new Set<string>();
  const rowCounters: Record<string, number> = {};
  const nodes: Node[] = [];
  const stepEdges: Edge[] = [];

  const nextRow = (type: string) => {
    const row = rowCounters[type] ?? 0;
    rowCounters[type] = row + 1;
    return row;
  };

  const emit = (id: string) => {
    const n = nodeById.get(id);
    if (!n || visible.has(id)) return;
    visible.add(id);

    const style = typeStyles[n.type] ?? { bg: '#f8fafc', border: '#cbd5e1', text: '#475569' };
    const guid = scenarioGuid(n.id);
    const kids = childrenOf.get(id) ?? [];
    const steps = n.type === 'scenario' ? (scenarioById.get(guid)?.steps ?? []) : [];
    const expandable = kids.length > 0 || steps.length > 0;
    const isOpen = expanded.has(id);

    nodes.push({
      id: n.id,
      position: { x: COLUMN[n.type] * 320, y: nextRow(n.type) * 92 },
      data: {
        label: (
          <div style={{ textAlign: 'left', display: 'flex', gap: 6, alignItems: 'flex-start' }}>
            {expandable && (
              <span style={{ fontSize: 11, lineHeight: '16px', opacity: 0.8, width: 10 }}>
                {isOpen ? '▾' : '▸'}
              </span>
            )}
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600, fontSize: 12 }}>{n.label}</div>
              {n.sublabel && <div style={{ fontSize: 10, opacity: 0.7 }}>{n.sublabel}</div>}
              {expandable && (
                <div style={{ fontSize: 10, opacity: 0.6, marginTop: 2 }}>
                  {isOpen
                    ? 'Click to collapse'
                    : n.type === 'scenario'
                      ? `Click to show steps · ${steps.length}`
                      : `Click to expand · ${kids.length}`}
                </div>
              )}
            </div>
          </div>
        ),
      },
      style: {
        background: style.bg,
        border: `1.5px solid ${style.border}`,
        borderRadius: 8,
        color: style.text,
        width: 240,
        padding: 8,
        fontSize: 12,
        cursor: expandable ? 'pointer' : 'default',
        boxShadow: expandable && isOpen ? `0 0 0 2px ${style.border}33` : undefined,
      },
      sourcePosition: Position.Right,
      targetPosition: Position.Left,
    });

    // Reveal children only when this node is expanded.
    if (!isOpen) return;

    // Reveal hierarchy children (modules/features/scenarios).
    for (const childId of kids) emit(childId);

    // Reveal the scenario's steps as a connected path: scenario → step 1 → step 2 → …
    const stepStyle = typeStyles.step;
    let prevId = id;
    steps.forEach((s, i) => {
      const stepId = `step:${guid}:${i}`;
      nodes.push({
        id: stepId,
        position: { x: COLUMN.step * 320, y: nextRow('step') * 92 },
        data: {
          label: (
            <div style={{ textAlign: 'left' }}>
              <div style={{ fontSize: 12 }}>
                <span style={{ opacity: 0.6, marginRight: 4 }}>{i + 1}.</span>{s.action}
              </div>
              {s.expectedResult && (
                <div style={{ fontSize: 10, opacity: 0.7, marginTop: 2 }}>→ {s.expectedResult}</div>
              )}
            </div>
          ),
        },
        style: {
          background: stepStyle.bg,
          border: `1.5px solid ${stepStyle.border}`,
          borderRadius: 8,
          color: stepStyle.text,
          width: 240,
          padding: 8,
          fontSize: 12,
        },
        sourcePosition: Position.Bottom,
        targetPosition: Position.Top,
      });
      stepEdges.push({
        id: `stepedge-${prevId}-${stepId}`,
        source: prevId,
        target: stepId,
        animated: true,
        style: { stroke: stepStyle.border, strokeWidth: 1.5 },
      });
      prevId = stepId;
    });
  };

  for (const r of roots) emit(r.id);

  const hierarchyEdges: Edge[] = graph.edges
    .filter((e) => visible.has(e.source) && visible.has(e.target))
    .map((e) => ({
      id: e.id,
      source: e.source,
      target: e.target,
      animated: true,
      style: { stroke: '#a78bfa', strokeWidth: 1.5 },
    }));

  return { nodes, edges: [...hierarchyEdges, ...stepEdges] };
}

export function KnowledgeGraphTab({ projectId }: { projectId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['knowledge-graph', projectId],
    queryFn: () => getKnowledgeGraph(projectId),
  });

  // Scenarios (with their steps) so clicking a scenario node can show its steps.
  const { data: scenarios = [] } = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
  });

  const scenarioById = useMemo(() => {
    const m = new Map<string, Scenario>();
    for (const s of scenarios) m.set(s.id, s);
    return m;
  }, [scenarios]);

  // Expansion state: seed with the roots (project) so modules are visible from the start.
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!data) return;
    const hasParent = new Set(data.edges.map((e) => e.target));
    setExpanded(new Set(data.nodes.filter((n) => !hasParent.has(n.id)).map((n) => n.id)));
  }, [data]);

  const expandableIds = useMemo(() => {
    const s = new Set<string>();
    if (!data) return s;
    for (const e of data.edges) s.add(e.source);          // modules/features (and project)
    for (const n of data.nodes) {                          // scenarios that have steps
      if (n.type === 'scenario' && (scenarioById.get(scenarioGuid(n.id))?.steps.length ?? 0) > 0) {
        s.add(n.id);
      }
    }
    return s;
  }, [data, scenarioById]);

  const onNodeClick = useCallback((_: unknown, node: Node) => {
    if (!expandableIds.has(node.id)) return; // step nodes / empty scenarios aren't expandable
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(node.id)) {
        next.delete(node.id);
      } else {
        // Only one scenario's steps are shown at a time: opening a scenario collapses any other.
        if (node.id.startsWith('scenario:')) {
          for (const id of [...next]) if (id.startsWith('scenario:')) next.delete(id);
        }
        next.add(node.id);
      }
      return next;
    });
  }, [expandableIds]);

  const { nodes, edges } = useMemo(
    () => (data ? buildGraph(data, expanded, scenarioById) : { nodes: [], edges: [] }),
    [data, expanded, scenarioById],
  );

  const hasContent = (data?.nodes.length ?? 0) > 1;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Knowledge Graph</CardTitle>
              <CardDescription>Click a module → its features, a feature → its scenarios, a scenario → its steps.</CardDescription>
            </div>
            <div className="flex items-center gap-1.5">
              {Object.entries(typeStyles).map(([type, style]) => (
                <span key={type}
                  className="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium border capitalize"
                  style={{ background: style.bg, borderColor: style.border, color: style.text }}>
                  {type}
                </span>
              ))}
            </div>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {!isLoading && !hasContent && (
            <div className="p-6">
              <Alert severity="info" className="text-sm">
                The graph is empty. Analyze a requirement and generate scenarios to populate it.
              </Alert>
            </div>
          )}
          <div style={{ height: 560 }} className="rounded-b-xl overflow-hidden border-t border-border">
            <ReactFlow
              nodes={nodes}
              edges={edges}
              onNodeClick={onNodeClick}
              fitView
              proOptions={{ hideAttribution: true }}
            >
              <Background color="#e2e8f0" gap={20} />
              <Controls />
            </ReactFlow>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
