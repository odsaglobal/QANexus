import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check, ChevronRight, Loader2, Minus, Printer, X } from 'lucide-react';
import { listSessionSteps, fileUrl } from '../../api/explorer';
import { listScenarios } from '../../api/scenarios';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import { AtipLogo } from '../../components/AtipLogo';
import { cn } from '../../lib/utils';
import { StepDetail, ValidationTree } from './ScenariosTab';
import type { ExplorationSession, SessionStepResult, StepRunStatus } from '../../api/types';

interface Props {
  session: ExplorationSession;
  projectId: string;
  title: string;
  projectName: string;
  open: boolean;
  onClose: () => void;
}

function formatDuration(startIso?: string | null, endIso?: string | null): string {
  if (!startIso) return '—';
  const ms = (endIso ? new Date(endIso).getTime() : Date.now()) - new Date(startIso).getTime();
  const secs = Math.max(0, Math.round(ms / 1000));
  if (secs < 60) return `${secs}s`;
  return `${Math.floor(secs / 60)}m ${secs % 60}s`;
}

/**
 * Printable execution report for a finished run: what ran, what was asserted, the verdict, and the
 * screenshot evidence behind each step. Deliberately not the live explorer — no browser frames, no
 * activity log, nothing that only means something while a run is in flight.
 *
 * "Download PDF" prints the report; the browser's print pipeline writes the PDF, which avoids
 * shipping a PDF renderer just to lay out a list of steps. See the @media print block in index.css.
 */
export function RunReportDialog({ session, projectId, title, projectName, open, onClose }: Props) {
  const [zoomed, setZoomed] = useState<SessionStepResult | null>(null);
  // Only the deviations from the default are stored, so the default can depend on how many
  // scenarios the run turns out to have without an effect to seed the state.
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});

  const stepsQuery = useQuery({
    queryKey: ['session-steps', projectId, session.id],
    queryFn: () => listSessionSteps(projectId, session.id),
    enabled: open,
  });

  const scenariosQuery = useQuery({
    queryKey: ['scenarios', projectId],
    queryFn: () => listScenarios(projectId),
    enabled: open,
  });

  const steps = useMemo(
    () => [...(stepsQuery.data ?? [])].sort((a, b) => a.stepOrder - b.stepOrder),
    [stepsQuery.data],
  );

  // A suite run interleaves several scenarios, so present the trace grouped rather than as one
  // flat list whose step numbers restart without explanation.
  const groups = useMemo(() => {
    const byScenario = new Map<string, { title: string; steps: SessionStepResult[] }>();
    for (const step of steps) {
      const group = byScenario.get(step.scenarioId) ?? { title: step.scenarioTitle, steps: [] };
      group.steps.push(step);
      byScenario.set(step.scenarioId, group);
    }
    return [...byScenario.entries()].map(([id, g]) => ({ id, ...g }));
  }, [steps]);

  // Step results carry no expected result, so pair each one with its authored step to recover the
  // validations. Keyed by scenario + order because orders restart per scenario.
  const expectedByStep = useMemo(() => {
    const map = new Map<string, string>();
    for (const scenario of scenariosQuery.data ?? []) {
      for (const step of scenario.steps) {
        if (step.expectedResult) map.set(`${scenario.id}:${step.order}`, step.expectedResult);
      }
    }
    return map;
  }, [scenariosQuery.data]);

  const passed = steps.filter((s) => s.status === 'Passed').length;
  const healed = steps.filter((s) => s.status === 'Healed').length;
  const failed = steps.filter((s) => s.status === 'Failed').length;
  const skipped = steps.filter((s) => s.status === 'Skipped').length;
  const loading = stepsQuery.isLoading || scenariosQuery.isLoading;
  const verdict = failed > 0 ? 'Failed' : healed > 0 ? 'Passed with healing' : 'Passed';

  // ── Dashboard ────────────────────────────────────────────────────────────────────────────
  // Steps and scenarios are charted separately because they answer different questions: one
  // flaky step out of forty is a rounding error on the step donut but a whole red row on the
  // scenario donut, and it's the scenario view that decides whether the build ships.
  // A healed step still passed, so it counts towards the rate and stays visible in the legend.
  const stepSegments = [
    { label: 'Passed' as Outcome, value: passed },
    { label: 'Healed' as Outcome, value: healed },
    { label: 'Failed' as Outcome, value: failed },
    { label: 'Skipped' as Outcome, value: skipped },
  ];
  const passRate = steps.length ? Math.round(((passed + healed) / steps.length) * 100) : 0;

  const scenarioOutcomes = useMemo(
    () => groups.map((g) => ({ id: g.id, outcome: summarize(g.steps).label })),
    [groups],
  );
  const scenarioSegments = OUTCOMES.map((label) => ({
    label,
    value: scenarioOutcomes.filter((o) => o.outcome === label).length,
  }));
  const scenarioPassRate = scenarioOutcomes.length
    ? Math.round(
        (scenarioOutcomes.filter((o) => o.outcome === 'Passed' || o.outcome === 'Healed').length /
          scenarioOutcomes.length) *
          100,
      )
    : 0;

  // The run only carries scenario ids, so the category axes (Positive/Negative, priority) come
  // from the authored scenarios. A scenario deleted since the run keeps its steps but loses its
  // metadata, hence the 'Unspecified' bucket rather than dropping the row.
  const categories = useMemo(() => {
    const meta = new Map((scenariosQuery.data ?? []).map((s) => [s.id, s]));
    const rows = scenarioOutcomes.map((o) => ({ scenario: meta.get(o.id), outcome: o.outcome }));
    return {
      byType: bucketBy(rows.map((r) => ({ ...r, category: r.scenario?.type })), TYPE_ORDER),
      byPriority: bucketBy(rows.map((r) => ({ ...r, category: r.scenario?.priority })), PRIORITY_ORDER),
    };
  }, [scenarioOutcomes, scenariosQuery.data]);

  const multiScenario = groups.length > 1;

  // A suite run's trace is long, so it opens as a scannable list of scenarios and their verdicts;
  // a single-scenario run has nothing to scan, so it opens expanded.
  const isOpen = (id: string) => openGroups[id] ?? groups.length === 1;
  const toggleGroup = (id: string) =>
    setOpenGroups((prev) => ({ ...prev, [id]: !isOpen(id) }));

  return (
    <>
      <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
        {/* p-0 + flex column: the letterhead stays pinned and only the body scrolls, otherwise a
            long trace clips against the dialog's max height. */}
        <DialogContent className="print-area flex max-h-[90vh] w-[min(56rem,95vw)] max-w-none flex-col gap-0 overflow-hidden bg-white p-0 text-slate-900">
          <DialogHeader className="sr-only">
            <DialogTitle>Execution report — {title}</DialogTitle>
          </DialogHeader>

          {/* ── Letterhead ── */}
          <header className="flex shrink-0 items-start justify-between gap-4 border-b-2 border-slate-900 px-6 py-4 pr-14">
            <div className="flex items-center gap-2.5">
              <AtipLogo size={28} />
              <div>
                <p className="text-[15px] font-semibold leading-tight">Test Execution Report</p>
                <p className="text-[11px] text-slate-500">{projectName}</p>
              </div>
            </div>
            <Button variant="outline" size="sm" className="no-print shrink-0" onClick={() => window.print()}>
              <Printer className="mr-1.5 h-4 w-4" /> Download PDF
            </Button>
          </header>

          <div className="report-body min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-4">
            {/* ── Identification ── */}
            <dl className="grid grid-cols-[auto_1fr_auto_1fr] items-baseline gap-x-4 gap-y-1.5 text-[12px]">
              <Field label="Scenario" value={title} />
              <Field label="Execution" value={`E-${session.id.slice(0, 8).toUpperCase()}`} mono />
              <Field
                label="Executed"
                value={session.startedAtUtc ? new Date(session.startedAtUtc).toLocaleString() : '—'}
              />
              <Field label="Duration" value={formatDuration(session.startedAtUtc, session.completedAtUtc)} />
            </dl>

            {/* ── Verdict + headline numbers ── */}
            <section className="break-inside-avoid flex items-stretch gap-4 rounded border border-slate-200 bg-slate-50 p-3">
              <div className="flex flex-col justify-center border-r border-slate-200 pr-4">
                <span className="text-[10px] uppercase tracking-wider text-slate-500">Result</span>
                <span
                  className={`text-lg font-semibold leading-tight ${
                    failed > 0 ? 'text-red-600' : healed > 0 ? 'text-amber-600' : 'text-green-600'
                  }`}
                >
                  {verdict}
                </span>
              </div>
              <div className="grid flex-1 grid-cols-4 gap-2 text-center">
                <Tally
                  label="Pass rate"
                  value={`${passRate}%`}
                  tone={failed > 0 ? 'text-red-600' : 'text-green-600'}
                />
                <Tally label="Scenarios" value={groups.length} />
                <Tally label="Steps" value={steps.length} />
                <Tally label="Failed" value={failed} tone={failed > 0 ? 'text-red-600' : undefined} />
              </div>
            </section>

            {/* ── Dashboard ── */}
            {!loading && steps.length > 0 && (
              <div className="space-y-3">
                <div className={cn('grid gap-3', multiScenario && 'md:grid-cols-2')}>
                  <ChartCard title="Step outcomes" subtitle={`${steps.length} steps executed`}>
                    <Donut segments={stepSegments} value={`${passRate}%`} caption="pass rate" />
                    <Legend segments={stepSegments} total={steps.length} />
                  </ChartCard>
                  {multiScenario && (
                    <ChartCard title="Scenario outcomes" subtitle={`${groups.length} test cases executed`}>
                      <Donut segments={scenarioSegments} value={`${scenarioPassRate}%`} caption="pass rate" />
                      <Legend segments={scenarioSegments} total={groups.length} />
                    </ChartCard>
                  )}
                </div>

                {multiScenario && (
                  <div className="grid gap-3 md:grid-cols-2">
                    <CategoryCard title="By test type" rows={categories.byType} />
                    <CategoryCard title="By priority" rows={categories.byPriority} />
                  </div>
                )}
              </div>
            )}

            {skipped > 0 && (
              <p className="text-[11px] text-slate-500">
                {skipped} step(s) were skipped after an earlier failure.
              </p>
            )}

            {session.errorMessage && (
              <p className="rounded border border-red-200 bg-red-50 p-2.5 text-[12px] text-red-700">
                {session.errorMessage}
              </p>
            )}

            {/* ── Step trace ── */}
            {loading ? (
              <p className="flex items-center justify-center gap-2 py-10 text-sm text-slate-500">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading report…
              </p>
            ) : steps.length === 0 ? (
              <p className="py-10 text-center text-sm text-slate-500">This run recorded no step results.</p>
            ) : (
              groups.map((group) => {
                const expanded = isOpen(group.id);
                const groupResult = summarize(group.steps);
                return (
                  <section key={group.id} className="overflow-hidden rounded border border-slate-200">
                    <button
                      type="button"
                      onClick={() => toggleGroup(group.id)}
                      aria-expanded={expanded}
                      className="flex w-full items-center gap-2 bg-slate-50 px-3 py-2 text-left transition hover:bg-slate-100"
                    >
                      <ChevronRight
                        className={cn('no-print h-3.5 w-3.5 shrink-0 text-slate-400 transition-transform', expanded && 'rotate-90')}
                      />
                      <span className="min-w-0 flex-1 truncate text-[12px] font-semibold text-slate-700">
                        {group.title}
                      </span>
                      <span className="shrink-0 text-[11px] tabular-nums text-slate-500">
                        {group.steps.length} step{group.steps.length === 1 ? '' : 's'}
                      </span>
                      <span className={cn('shrink-0 rounded-full border px-1.5 py-0.5 text-[10px] font-semibold', groupResult.tone)}>
                        {groupResult.label}
                      </span>
                    </button>
                    {/* A collapsed group still prints: the PDF has to carry the whole trace no
                        matter which sections the reader happened to have open on screen. */}
                    <ol className={cn('divide-y divide-slate-200 border-t border-slate-200 px-3', !expanded && 'hidden print:block')}>
                      {group.steps.map((step) => (
                        <StepRow
                          key={`${step.scenarioId}-${step.stepOrder}`}
                          step={step}
                          expected={expectedByStep.get(`${step.scenarioId}:${step.stepOrder}`)}
                          onZoom={() => setZoomed(step)}
                        />
                      ))}
                    </ol>
                  </section>
                );
              })
            )}

            <footer className="border-t border-slate-200 pt-2 text-[10px] text-slate-400">
              Generated by ATiP on {new Date().toLocaleString()} · Execution {session.id}
            </footer>
          </div>
        </DialogContent>
      </Dialog>

      {/* Thumbnails keep the report readable; the full capture stays one click away. */}
      <Dialog open={!!zoomed} onOpenChange={(v) => !v && setZoomed(null)}>
        <DialogContent className="flex max-h-[92vh] w-[min(80rem,95vw)] max-w-none flex-col overflow-hidden">
          <DialogHeader className="shrink-0 pr-8">
            <DialogTitle className="truncate text-sm font-normal">
              Step {zoomed?.stepOrder} — {zoomed?.action}
            </DialogTitle>
          </DialogHeader>
          {zoomed?.screenshotPath && (
            <img
              src={fileUrl(zoomed.screenshotPath)}
              alt={`Evidence for step ${zoomed.stepOrder}`}
              className="min-h-0 flex-1 rounded border border-border object-contain"
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

function StepRow({
  step,
  expected,
  onZoom,
}: {
  step: SessionStepResult;
  expected?: string;
  onZoom: () => void;
}) {
  return (
    <li className="break-inside-avoid flex items-start gap-3 py-2.5">
      <span className="w-4 shrink-0 pt-px text-right text-[12px] tabular-nums text-slate-400">
        {step.stepOrder}
      </span>
      <StepGlyph status={step.status} />

      <div className="min-w-0 flex-1 text-[12px]">
        <p className="leading-snug">{step.action}</p>
        {expected && (
          <ValidationTree
            expectedResult={expected}
            status={step.status as StepRunStatus}
            checks={step.checks}
          />
        )}
        {/* Redundant once each assertion reports its own actual value. */}
        {step.detail && !(step.checks && step.checks.length > 0) && (
          <StepDetail
            detail={step.detail}
            className={`mt-1 text-[11px] ${step.status === 'Failed' ? 'text-red-600' : 'text-slate-500'}`}
          />
        )}
        {step.url && <p className="mt-1 truncate text-[10px] text-slate-400">{step.url}</p>}
      </div>

      {step.screenshotPath && (
        <button
          type="button"
          onClick={onZoom}
          title="View full screenshot"
          className="report-thumb shrink-0 overflow-hidden rounded border border-slate-200 transition hover:border-slate-400"
        >
          <img
            src={fileUrl(step.screenshotPath)}
            alt={`Evidence for step ${step.stepOrder}`}
            className="h-[68px] w-[120px] object-cover object-top"
          />
        </button>
      )}
    </li>
  );
}

/** One-word verdict for a scenario, so a collapsed group still says how it went. */
function summarize(steps: SessionStepResult[]): { label: Outcome; tone: string } {
  if (steps.some((s) => s.status === 'Failed')) {
    return { label: 'Failed', tone: 'border-red-200 bg-red-50 text-red-700' };
  }
  if (steps.some((s) => s.status === 'Healed')) {
    return { label: 'Healed', tone: 'border-amber-200 bg-amber-50 text-amber-700' };
  }
  if (steps.every((s) => s.status === 'Skipped')) {
    return { label: 'Skipped', tone: 'border-slate-200 bg-slate-100 text-slate-500' };
  }
  return { label: 'Passed', tone: 'border-green-200 bg-green-50 text-green-700' };
}

// ── Charts ─────────────────────────────────────────────────────────────────────────────────
// Hand-rolled SVG/flex rather than a charting library: these are four-slice donuts and stacked
// bars on a document that has to print, and a library's responsive containers measure themselves
// against the on-screen dialog, not the paper.

const OUTCOMES = ['Passed', 'Healed', 'Failed', 'Skipped'] as const;
type Outcome = (typeof OUTCOMES)[number];

/** Literal hex, not Tailwind classes: these feed SVG fills and inline widths that must survive print. */
const OUTCOME_COLOR: Record<Outcome, string> = {
  Passed: '#16a34a',
  Healed: '#d97706',
  Failed: '#dc2626',
  Skipped: '#cbd5e1',
};

const TYPE_ORDER = [
  'Positive', 'Negative', 'Boundary', 'Regression', 'Smoke',
  'Sanity', 'Accessibility', 'Security', 'Api', 'CrossBrowser', 'Performance',
];
const PRIORITY_ORDER = ['Critical', 'High', 'Medium', 'Low'];

interface Segment {
  label: Outcome;
  value: number;
}

interface CategoryRow {
  label: string;
  counts: Record<Outcome, number>;
  total: number;
}

/** Tally scenario outcomes per category, ordered by the domain enum so Positive precedes Negative. */
function bucketBy(
  rows: { category?: string; outcome: Outcome }[],
  order: string[],
): CategoryRow[] {
  const byCategory = new Map<string, CategoryRow>();
  for (const row of rows) {
    const label = row.category || 'Unspecified';
    const bucket = byCategory.get(label) ?? {
      label,
      counts: { Passed: 0, Healed: 0, Failed: 0, Skipped: 0 },
      total: 0,
    };
    bucket.counts[row.outcome] += 1;
    bucket.total += 1;
    byCategory.set(label, bucket);
  }
  const rank = (label: string) => {
    const i = order.indexOf(label);
    return i === -1 ? order.length : i;
  };
  return [...byCategory.values()].sort((a, b) => rank(a.label) - rank(b.label) || b.total - a.total);
}

function ChartCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="break-inside-avoid rounded border border-slate-200 p-3">
      <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">{title}</p>
      {subtitle && <p className="mt-0.5 text-[10px] text-slate-400">{subtitle}</p>}
      <div className="mt-2 flex items-center gap-4">{children}</div>
    </section>
  );
}

function Donut({ segments, value, caption }: { segments: Segment[]; value: string; caption: string }) {
  const size = 104;
  const stroke = 13;
  const radius = (size - stroke) / 2;
  const circumference = 2 * Math.PI * radius;
  const total = segments.reduce((sum, s) => sum + s.value, 0);

  // Pre-compute offsets so each arc starts where the previous one ended.
  let consumed = 0;
  const arcs = segments
    .filter((s) => s.value > 0)
    .map((s) => {
      const length = (s.value / total) * circumference;
      const arc = { ...s, length, offset: consumed };
      consumed += length;
      return arc;
    });

  return (
    <svg
      width={size}
      height={size}
      viewBox={`0 0 ${size} ${size}`}
      className="shrink-0"
      role="img"
      aria-label={segments.map((s) => `${s.value} ${s.label.toLowerCase()}`).join(', ')}
    >
      <g transform={`rotate(-90 ${size / 2} ${size / 2})`}>
        <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="#e2e8f0" strokeWidth={stroke} />
        {arcs.map((arc) => (
          <circle
            key={arc.label}
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke={OUTCOME_COLOR[arc.label]}
            strokeWidth={stroke}
            strokeDasharray={`${arc.length} ${circumference - arc.length}`}
            strokeDashoffset={-arc.offset}
          />
        ))}
      </g>
      {/* Baselines, not centres: the value sits just above the midline so value + caption read as
          one optically centred block. */}
      <text x={size / 2} y={size / 2 + 3} textAnchor="middle" fontSize={20} fontWeight={600} fill="#0f172a">
        {value}
      </text>
      <text x={size / 2} y={size / 2 + 17} textAnchor="middle" fontSize={9} fill="#94a3b8">
        {caption}
      </text>
    </svg>
  );
}

function Legend({ segments, total }: { segments: Segment[]; total: number }) {
  return (
    <dl className="min-w-0 flex-1 space-y-1.5">
      {segments.map((s) => (
        <div key={s.label} className="flex items-center gap-2 text-[11px]">
          <span
            className="h-2.5 w-2.5 shrink-0 rounded-sm"
            style={{ backgroundColor: OUTCOME_COLOR[s.label] }}
          />
          <dt className="flex-1 text-slate-600">{s.label}</dt>
          <dd className="tabular-nums font-medium text-slate-900">{s.value}</dd>
          <dd className="w-9 text-right tabular-nums text-slate-400">
            {total ? Math.round((s.value / total) * 100) : 0}%
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** Stacked pass/fail bar per category — which kinds of test are actually failing. */
function CategoryCard({ title, rows }: { title: string; rows: CategoryRow[] }) {
  return (
    <section className="break-inside-avoid rounded border border-slate-200 p-3">
      <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">{title}</p>
      <div className="mt-2.5 space-y-2">
        {rows.map((row) => {
          const rate = Math.round(((row.counts.Passed + row.counts.Healed) / row.total) * 100);
          return (
            <div key={row.label} className="grid grid-cols-[6.5rem_minmax(0,1fr)_3.75rem] items-center gap-2">
              <span className="truncate text-[11px] text-slate-600" title={row.label}>
                {row.label}
              </span>
              <div className="flex h-2.5 overflow-hidden rounded-full bg-slate-100">
                {OUTCOMES.filter((o) => row.counts[o] > 0).map((o) => (
                  <div
                    key={o}
                    title={`${row.counts[o]} ${o.toLowerCase()}`}
                    style={{
                      width: `${(row.counts[o] / row.total) * 100}%`,
                      backgroundColor: OUTCOME_COLOR[o],
                    }}
                  />
                ))}
              </div>
              <span className="text-right text-[11px] tabular-nums text-slate-700">
                <span className="font-medium">{rate}%</span>
                <span className="text-slate-400"> · {row.total}</span>
              </span>
            </div>
          );
        })}
      </div>
    </section>
  );
}

function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <>
      <dt className="text-[10px] uppercase tracking-wider text-slate-500">{label}</dt>
      <dd className={`min-w-0 truncate font-medium ${mono ? 'font-mono text-[11px]' : ''}`}>{value}</dd>
    </>
  );
}

function Tally({ label, value, tone }: { label: string; value: number | string; tone?: string }) {
  return (
    <div>
      <p className={`text-lg font-semibold leading-tight ${tone ?? 'text-slate-900'}`}>{value}</p>
      <p className="text-[10px] uppercase tracking-wider text-slate-500">{label}</p>
    </div>
  );
}

function StepGlyph({ status }: { status: string }) {
  if (status === 'Failed') return <X className="mt-px h-4 w-4 shrink-0 text-red-600" strokeWidth={3} />;
  if (status === 'Skipped') return <Minus className="mt-px h-4 w-4 shrink-0 text-slate-300" strokeWidth={3} />;
  return (
    <Check
      className={`mt-px h-4 w-4 shrink-0 ${status === 'Healed' ? 'text-amber-600' : 'text-green-600'}`}
      strokeWidth={3}
    />
  );
}
