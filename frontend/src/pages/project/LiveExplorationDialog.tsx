import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Square } from 'lucide-react';
import { useExplorationStream } from '../../lib/useExplorationStream';
import { cancelExploration } from '../../api/explorer';
import { cn } from '../../lib/utils';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../../components/ui/dialog';

const stepStatusVariant: Record<string, 'success' | 'warning' | 'destructive' | 'secondary'> = {
  Passed: 'success', Healed: 'warning', Failed: 'destructive', Skipped: 'secondary',
};

const logLevelColor: Record<string, string> = {
  info: 'text-slate-300',
  success: 'text-green-400',
  warn: 'text-amber-400',
  error: 'text-red-400',
};

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
  const { frame, status, steps, logs, connected } = useExplorationStream(sessionId, open && !!sessionId);
  const logEndRef = useRef<HTMLDivElement | null>(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ block: 'end' });
  }, [logs.length]);

  const terminal = status?.status === 'Completed' || status?.status === 'Failed' || status?.status === 'Cancelled';
  const verb = mode === 'run' ? 'Running' : 'Exploring';

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
          {/* Live browser stream */}
          <div className="min-w-0 overflow-hidden rounded-lg border border-border">
            <div className="flex items-center justify-between px-3 py-2 bg-gray-50">
              <span className="text-xs font-semibold text-foreground">Live browser</span>
              <span className="text-xs text-muted-foreground truncate max-w-[360px]">
                {status?.currentUrl ?? (connected ? 'Waiting for first frame…' : 'Connecting…')}
              </span>
            </div>
            <div className="relative bg-slate-900 aspect-video flex items-center justify-center max-h-[72vh]">
              {frame ? (
                <img src={`data:image/jpeg;base64,${frame}`} alt="Live exploration" className="w-full h-full object-contain" />
              ) : (
                <div className="flex flex-col items-center gap-2 text-slate-400">
                  <Loader2 className="h-6 w-6 animate-spin" />
                  <p className="text-xs">{connected ? 'Streaming will begin shortly…' : 'Connecting to live stream…'}</p>
                </div>
              )}
            </div>
          </div>

          {/* Right column: activity log above the steps */}
          <div className="flex min-w-0 flex-col gap-3">
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

            {/* Steps discovered/executed */}
            <div className="overflow-hidden rounded-lg border border-border">
              <div className="px-3 py-2 bg-gray-50 text-xs font-semibold text-foreground">
                Steps <span className="text-muted-foreground font-normal">({steps.length})</span>
              </div>
              <div className="max-h-[28vh] overflow-y-auto divide-y divide-border">
                {steps.length === 0 ? (
                  <p className="px-4 py-6 text-center text-xs text-muted-foreground">Waiting for the first step…</p>
                ) : (
                  steps.map((s, i) => (
                    <div key={`${s.scenarioId}-${s.stepOrder}-${i}`} className="flex items-start gap-3 px-4 py-2.5">
                      <Badge variant={stepStatusVariant[s.status] ?? 'secondary'} className="mt-0.5 shrink-0">{s.status}</Badge>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm text-foreground">
                          <span className="text-muted-foreground mr-1.5">{s.stepOrder}.</span>{s.action}
                        </p>
                        {s.detail && <p className="text-xs text-muted-foreground mt-0.5">{s.detail}</p>}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
