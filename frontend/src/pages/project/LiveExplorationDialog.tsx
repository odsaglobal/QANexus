import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Square, X, ZoomIn, Globe } from 'lucide-react';
import { useExplorationStream } from '../../lib/useExplorationStream';
import type { ExplorationLiveTab } from '../../lib/useExplorationStream';
import { cancelExploration, fileUrl, listSessionSteps } from '../../api/explorer';
import { cn } from '../../lib/utils';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import type { SessionStepResult } from '../../api/types';

const stepStatusVariant: Record<string, 'success' | 'warning' | 'destructive' | 'secondary'> = {
  Passed: 'success', Healed: 'warning', Failed: 'destructive', Skipped: 'secondary', Running: 'secondary',
};

const logLevelColor: Record<string, string> = {
  info: 'text-slate-300',
  success: 'text-green-400',
  warn: 'text-amber-400',
  error: 'text-red-400',
  // An action that has been dispatched but not yet verified — dimmed until its outcome rewrites it.
  pending: 'text-slate-400',
};

/**
 * A test's verdict is the WORST outcome among its steps — one failed step fails the test however
 * many passed around it. Mirrors the ranking the server applies when it streams the same verdict.
 */
const statusRank: Record<string, number> = { Passed: 0, Healed: 1, Skipped: 2, Failed: 3 };

function verdictOf(statuses: string[]): string {
  if (statuses.includes('Running')) return 'Running';
  return statuses.reduce(
    (worst, s) => ((statusRank[s] ?? 0) > (statusRank[worst] ?? 0) ? s : worst),
    statuses[0] ?? 'Skipped',
  );
}

interface Props {
  sessionId: string | null;
  projectId: string;
  title: string;
  mode?: 'explore' | 'run';
  open: boolean;
  onClose: () => void;
}

/**
 * Live view of a scenario/suite exploration: streams the browser frames, activity log
 * and per-step results over SignalR for as long as the dialog is open.
 */
export function LiveExplorationDialog({ sessionId, projectId, title, mode = 'explore', open, onClose }: Props) {
  const { frame, status, steps: liveSteps, scenarios, logs, tabs, connected } = useExplorationStream(sessionId, open && !!sessionId);
  const logEndRef = useRef<HTMLDivElement | null>(null);
  const queryClient = useQueryClient();
  const [zoomSrc, setZoomSrc] = useState<string | null>(null);

  const terminal = status?.status === 'Completed' || status?.status === 'Failed' || status?.status === 'Cancelled';
  const verb = mode === 'run' ? 'Running' : 'Exploring';

  // Persisted (DB-backed) step results — the only source that survives past a terminal session and the
  // only source that carries screenshots. Polls while the run is in progress, keeps the final list once done.
  const stepsQuery = useQuery({
    queryKey: ['session-steps', projectId, sessionId],
    queryFn: () => listSessionSteps(projectId, sessionId!),
    enabled: open && !!sessionId,
    refetchInterval: terminal ? false : 2500,
  });
  const persistedSteps = stepsQuery.data ?? [];

  // The final steps are written in the same instant the session turns terminal, so the last poll of the
  // 2.5s interval almost always lands JUST BEFORE them — and flipping to terminal switches polling off,
  // freezing the list one step short. Force one more read after the run ends.
  const { refetch: refetchSteps } = stepsQuery;
  useEffect(() => {
    if (terminal) void refetchSteps();
  }, [terminal, refetchSteps]);

  // Prefer persisted results (richer: screenshot + survives session end), but keep appending any live
  // step the DB poll hasn't caught up with yet — including the one currently executing. Without that the
  // panel trailed the browser by up to a full poll interval plus the step's own verification time.
  const displaySteps: Array<SessionStepResult | (typeof liveSteps)[number]> = useMemo(() => {
    if (persistedSteps.length === 0) return terminal ? liveSteps.filter((s) => s.status !== 'Running') : liveSteps;
    const seen = new Set(persistedSteps.map((s) => `${s.scenarioId}|${s.stepOrder}`));
    const unsaved = liveSteps.filter(
      (s) => !seen.has(`${s.scenarioId}|${s.stepOrder}`) && !(terminal && s.status === 'Running'),
    );
    return [...persistedSteps, ...unsaved];
  }, [persistedSteps, liveSteps, terminal]);

  const groupedSteps = useMemo(() => {
    const groups: { scenarioId: string; scenarioTitle: string; items: typeof displaySteps }[] = [];
    for (const step of displaySteps) {
      const scenarioTitle = 'scenarioTitle' in step ? step.scenarioTitle : undefined;
      let group = groups.find((g) => g.scenarioId === step.scenarioId);
      if (!group) {
        group = { scenarioId: step.scenarioId, scenarioTitle: scenarioTitle ?? '', items: [] };
        groups.push(group);
      }
      group.items.push(step);
    }
    return groups;
  }, [displaySteps]);
  const showScenarioHeaders = groupedSteps.length > 1;

  /**
   * One row per test in the run. Prefers the streamed progress events (they know the suite's true
   * length up front, so pending tests are listed before they start), and falls back to deriving rows
   * from the persisted steps — needed when the dialog is opened on a finished run, whose replay
   * buffer the server has already released.
   */
  const testProgress = useMemo(() => {
    if (scenarios.length > 0) {
      return [...scenarios].sort((a, b) => a.index - b.index);
    }
    return groupedSteps.map((group, i) => ({
      scenarioId: group.scenarioId,
      scenarioTitle: group.scenarioTitle,
      index: i + 1,
      total: groupedSteps.length,
      stepCount: group.items.length,
      status: verdictOf(group.items.map((s) => s.status)),
    }));
  }, [scenarios, groupedSteps]);

  const totalTests = testProgress[0]?.total ?? testProgress.length;
  const finishedTests = testProgress.filter((t) => t.status !== 'Running').length;
  const currentTest = testProgress.find((t) => t.status === 'Running');
  const verdictByScenario = useMemo(
    () => new Map(testProgress.map((t) => [t.scenarioId, t.status])),
    [testProgress],
  );

  // Once live streaming has ended (or never started, e.g. dialog opened directly on a completed run),
  // fall back to the last persisted step's screenshot so the browser panel still shows something real.
  const lastScreenshotPath = [...persistedSteps].reverse().find((s) => s.screenshotPath)?.screenshotPath;
  const staticPreview = !frame && lastScreenshotPath ? fileUrl(lastScreenshotPath) : null;

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ block: 'end' });
  }, [logs.length]);

  const stopMutation = useMutation({
    mutationFn: () => cancelExploration(projectId, sessionId!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] });
    },
  });

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-[96vw] w-[96vw] max-h-[94vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className={cn('h-2 w-2 rounded-full', terminal ? 'bg-gray-400' : connected ? 'bg-green-500 animate-pulse' : 'bg-gray-300')} />
            {verb}: {title}
            {totalTests > 1 && (
              <span className="text-xs font-normal text-muted-foreground">
                · {currentTest ? `test ${currentTest.index} of ${totalTests}` : `${finishedTests} of ${totalTests} complete`}
              </span>
            )}
            {!terminal && sessionId && (
              <Button
                variant="destructive"
                size="sm"
                className="ml-auto mr-6 h-7"
                disabled={stopMutation.isPending}
                onClick={() => stopMutation.mutate()}
              >
                {stopMutation.isPending
                  ? <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                  : <Square className="h-3.5 w-3.5 mr-1.5" />}
                {stopMutation.isPending ? 'Stopping…' : 'Stop'}
              </Button>
            )}
          </DialogTitle>
        </DialogHeader>

        <div className="grid gap-3 lg:grid-cols-[1.7fr_1fr] lg:items-start">
          {/* Live browser stream (falls back to the last persisted step screenshot once streaming ends) */}
          <div className="min-w-0 overflow-hidden rounded-lg border border-border">
            <div className="flex items-center justify-between px-3 py-2 bg-gray-50">
              <span className="text-xs font-semibold text-foreground">{frame ? 'Live browser' : staticPreview ? 'Last captured screenshot' : 'Live browser'}</span>
              <span className="text-xs text-muted-foreground truncate max-w-[360px]">
                {status?.currentUrl ?? (connected ? 'Waiting for first frame…' : 'Connecting…')}
              </span>
            </div>
            <LiveTabsBar tabs={tabs} />
            <div className="relative bg-slate-900 aspect-video flex items-center justify-center max-h-[72vh]">
              {frame ? (
                <img src={`data:image/jpeg;base64,${frame}`} alt="Live exploration" className="w-full h-full object-contain" />
              ) : staticPreview ? (
                <button
                  type="button"
                  className="group relative h-full w-full cursor-zoom-in"
                  onClick={() => setZoomSrc(staticPreview)}
                >
                  <img src={staticPreview} alt="Last captured step screenshot" className="w-full h-full object-contain" />
                  <span className="absolute inset-0 hidden items-center justify-center bg-black/30 group-hover:flex">
                    <ZoomIn className="h-6 w-6 text-white" />
                  </span>
                </button>
              ) : (
                <div className="flex flex-col items-center gap-2 text-slate-400">
                  <Loader2 className="h-6 w-6 animate-spin" />
                  <p className="text-xs">{connected ? 'Streaming will begin shortly…' : 'Connecting to live stream…'}</p>
                </div>
              )}
            </div>
          </div>

          {/* Right column: test progress, activity log, then the steps of each test */}
          <div className="flex min-w-0 flex-col gap-3">
            {/* Which test the run is on. Only meaningful for a suite — a single scenario is its own progress. */}
            {totalTests > 1 && (
              <div className="overflow-hidden rounded-lg border border-border">
                <div className="flex items-center justify-between gap-2 bg-gray-50 px-3 py-2">
                  <span className="text-xs font-semibold text-foreground">Tests</span>
                  <span className="text-xs text-muted-foreground">{finishedTests} of {totalTests} complete</span>
                </div>
                <div className="h-1 w-full bg-gray-100">
                  <div
                    className="h-full bg-violet-600 transition-[width] duration-500"
                    style={{ width: `${Math.round((finishedTests / totalTests) * 100)}%` }}
                  />
                </div>
                <ol className="max-h-[24vh] divide-y divide-border overflow-y-auto">
                  {testProgress.map((t) => (
                    <li
                      key={t.scenarioId}
                      className={cn(
                        'flex items-center gap-2.5 px-3 py-2',
                        t.status === 'Running' && 'bg-violet-50',
                      )}
                    >
                      <span className="w-4 shrink-0 text-right text-xs tabular-nums text-muted-foreground">{t.index}</span>
                      <span className="min-w-0 flex-1 truncate text-xs text-foreground" title={t.scenarioTitle}>
                        {t.scenarioTitle || 'Scenario'}
                      </span>
                      <span className="shrink-0 text-[11px] text-muted-foreground">{t.stepCount} step{t.stepCount === 1 ? '' : 's'}</span>
                      <Badge
                        variant={stepStatusVariant[t.status] ?? 'secondary'}
                        className={cn('shrink-0', t.status === 'Running' && 'animate-pulse')}
                      >
                        {t.status}
                      </Badge>
                    </li>
                  ))}
                  {/* The suite's length is known from the first event, so the tests still queued are
                      accounted for even before they produce any rows of their own. */}
                  {testProgress.length < totalTests && (
                    <li className="px-3 py-2 text-xs text-muted-foreground">
                      {totalTests - testProgress.length} test{totalTests - testProgress.length === 1 ? '' : 's'} queued
                    </li>
                  )}
                </ol>
              </div>
            )}

            {/* Activity log */}
            <div className="overflow-hidden rounded-lg border border-border">
              <div className="px-3 py-2 bg-gray-50 text-xs font-semibold text-foreground">
                Exploration log <span className="text-muted-foreground font-normal">({logs.length})</span>
              </div>
              <div className="h-[38vh] overflow-y-auto bg-slate-900 px-3 py-2 font-mono text-xs leading-relaxed">
                {logs.length === 0 ? (
                  <p className="text-slate-400 py-2">{connected ? 'Waiting for activity…' : 'Connecting to live log…'}</p>
                ) : (
                  logs.map((l, i) => (
                    <div key={i} className="flex gap-2">
                      <span className="text-slate-500 shrink-0">{new Date(l.timestampUtc).toLocaleTimeString()}</span>
                      <span className={cn('break-all', logLevelColor[l.level] ?? 'text-slate-300')}>{l.message}</span>
                    </div>
                  ))
                )}
                <div ref={logEndRef} />
              </div>
            </div>

            {/* Steps executed, each with its screenshot + validation detail */}
            <div className="overflow-hidden rounded-lg border border-border">
              <div className="px-3 py-2 bg-gray-50 text-xs font-semibold text-foreground">
                Steps <span className="text-muted-foreground font-normal">({displaySteps.length})</span>
              </div>
              <div className="max-h-[50vh] overflow-y-auto divide-y divide-border">
                {displaySteps.length === 0 ? (
                  <p className="px-4 py-6 text-center text-xs text-muted-foreground">
                    {stepsQuery.isLoading ? 'Loading step results…' : 'Waiting for the first step…'}
                  </p>
                ) : (
                  groupedSteps.map((group) => (
                    <div key={group.scenarioId}>
                      {showScenarioHeaders && (
                        <div className="sticky top-0 flex items-center gap-2 bg-violet-50 px-4 py-1.5 text-xs font-semibold text-violet-800">
                          <span className="min-w-0 flex-1 truncate">{group.scenarioTitle || 'Scenario'}</span>
                          {verdictByScenario.get(group.scenarioId) && (
                            <Badge
                              variant={stepStatusVariant[verdictByScenario.get(group.scenarioId)!] ?? 'secondary'}
                              className={cn('shrink-0', verdictByScenario.get(group.scenarioId) === 'Running' && 'animate-pulse')}
                            >
                              {verdictByScenario.get(group.scenarioId)}
                            </Badge>
                          )}
                        </div>
                      )}
                      {group.items.map((s, i) => {
                        const screenshotPath = 'screenshotPath' in s ? s.screenshotPath : undefined;
                        return (
                          <div key={`${s.scenarioId}-${s.stepOrder}-${i}`} className="flex items-start gap-3 px-4 py-2.5">
                            <Badge variant={stepStatusVariant[s.status] ?? 'secondary'} className={cn('mt-0.5 shrink-0', s.status === 'Running' && 'animate-pulse')}>{s.status}</Badge>
                            <div className="min-w-0 flex-1">
                              <p className="text-sm text-foreground">
                                <span className="text-muted-foreground mr-1.5">{s.stepOrder}.</span>{s.action}
                              </p>
                              {s.detail && <p className="text-xs text-muted-foreground mt-0.5">{s.detail}</p>}
                            </div>
                            {screenshotPath && (
                              <button
                                type="button"
                                className="group relative shrink-0 overflow-hidden rounded border border-border"
                                onClick={() => setZoomSrc(fileUrl(screenshotPath))}
                              >
                                <img src={fileUrl(screenshotPath)} alt={`Step ${s.stepOrder} screenshot`} className="h-14 w-20 object-cover" />
                                <span className="absolute inset-0 hidden items-center justify-center bg-black/30 group-hover:flex">
                                  <ZoomIn className="h-4 w-4 text-white" />
                                </span>
                              </button>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>

        {zoomSrc && (
          <div
            className="fixed inset-0 z-[100] flex items-center justify-center bg-black/80 p-6"
            onClick={() => setZoomSrc(null)}
          >
            <button
              type="button"
              className="absolute right-6 top-6 rounded-full bg-white/10 p-1.5 text-white hover:bg-white/20"
              onClick={() => setZoomSrc(null)}
            >
              <X className="h-5 w-5" />
            </button>
            <img src={zoomSrc} alt="Step screenshot enlarged" className="max-h-full max-w-full object-contain" onClick={(e) => e.stopPropagation()} />
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

/**
 * Browser-style strip of the tabs the automated session currently has open, with the one being
 * driven highlighted. Shown even for a single tab so the viewer always has the same chrome to read;
 * a strip that only materialises during a popup is easy to miss precisely when it matters.
 */
export function LiveTabsBar({ tabs }: { tabs: ExplorationLiveTab[] }) {
  if (tabs.length === 0) return null;

  return (
    <div className="flex items-end gap-1 border-b border-border bg-gray-100 px-2 pt-1.5" role="tablist">
      {tabs.map((tab) => (
        <div
          key={tab.index}
          role="tab"
          aria-selected={tab.isActive}
          title={`${tab.title || 'Untitled'}\n${tab.url}`}
          className={cn(
            'flex max-w-[220px] min-w-0 items-center gap-1.5 rounded-t-md border border-b-0 px-2.5 py-1.5 text-xs',
            tab.isActive
              ? 'border-border bg-white font-medium text-foreground'
              : 'border-transparent bg-gray-200/70 text-muted-foreground',
          )}
        >
          <Globe className={cn('h-3 w-3 shrink-0', tab.isActive ? 'text-violet-600' : 'text-muted-foreground')} />
          <span className="truncate">{tab.title || hostOf(tab.url)}</span>
          {tab.isActive && <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-violet-600" />}
        </div>
      ))}
    </div>
  );
}

function hostOf(url: string): string {
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
}
