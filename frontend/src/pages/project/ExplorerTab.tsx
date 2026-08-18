import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Play, Square, RefreshCw, ExternalLink, Loader2 } from 'lucide-react';
import type { DiscoveredPage, ExplorationSession, ExplorationStatus } from '../../api/types';
import {
  cancelExploration, fileUrl, listDiscoveredPages, listExplorationSessions, startExploration,
} from '../../api/explorer';
import { listEnvironments } from '../../api/environments';
import { getErrorMessage } from '../../lib/apiClient';
import { useExplorationStream } from '../../lib/useExplorationStream';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { Separator } from '../../components/ui/separator';
import { cn } from '../../lib/utils';

const statusVariant: Record<ExplorationStatus, 'info' | 'warning' | 'success' | 'destructive' | 'secondary'> = {
  Pending: 'info', Running: 'warning', Completed: 'success', Failed: 'destructive', Cancelled: 'secondary',
} as any;

export function ExplorerTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selectedSession, setSelectedSession] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data: sessions = [], refetch: refetchSessions } = useQuery({
    queryKey: ['exploration-sessions', projectId],
    queryFn: () => listExplorationSessions(projectId),
    refetchInterval: (query) => {
      const data = query.state.data as ExplorationSession[] | undefined;
      return data?.some((s) => s.status === 'Running' || s.status === 'Pending') ? 4000 : false;
    },
  });

  useEffect(() => {
    if (sessions.length > 0 && !selectedSession) setSelectedSession(sessions[0].id);
  }, [sessions, selectedSession]);

  const cancelMutation = useMutation({
    mutationFn: (sessionId: string) => cancelExploration(projectId, sessionId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] }),
  });

  const activeSession = sessions.find((s) => s.id === selectedSession);

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Application Explorer</CardTitle>
              <CardDescription>AI-guided headless browser crawl — discovers pages, elements and locators automatically.</CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground" onClick={() => refetchSessions()}>
                <RefreshCw className="h-4 w-4" />
              </Button>
              <Button size="sm" onClick={() => setDialogOpen(true)}>
                <Play className="h-4 w-4 mr-1.5" /> Start exploration
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {error && <Alert severity="error" className="mb-4 text-sm">{error}</Alert>}

          {sessions.length === 0 ? (
            <Alert severity="info" className="text-sm">
              No explorations yet. Click "Start exploration" to let the AI crawl your application.
            </Alert>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-[260px_1fr] gap-4">
              {/* Session list */}
              <div className="space-y-1">
                <p className="text-xs font-semibold text-muted-foreground px-1 mb-2">Sessions</p>
                {sessions.map((session) => (
                  <div key={session.id}
                    onClick={() => setSelectedSession(session.id)}
                    className={cn(
                      'rounded-lg p-2.5 cursor-pointer transition-colors border',
                      selectedSession === session.id ? 'border-violet-200 bg-violet-50' : 'border-transparent hover:bg-gray-50',
                    )}>
                    <div className="flex items-center justify-between mb-1">
                      <Badge variant={statusVariant[session.status] as any}>{session.status}</Badge>
                      {(session.status === 'Running' || session.status === 'Pending') && (
                        <Button variant="ghost" size="icon" className="h-6 w-6"
                          onClick={(e) => { e.stopPropagation(); cancelMutation.mutate(session.id); }}>
                          <Square className="h-3 w-3" />
                        </Button>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground">{session.pagesDiscovered} pages · {session.elementsDiscovered} elements</p>
                    <p className="text-xs text-muted-foreground">{new Date(session.createdAtUtc).toLocaleString()}</p>
                  </div>
                ))}
              </div>

              {activeSession && <SessionDetail projectId={projectId} session={activeSession} />}
            </div>
          )}
        </CardContent>
      </Card>

      <StartExplorationDialog projectId={projectId} open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onStarted={(id) => {
          setDialogOpen(false);
          setSelectedSession(id);
          queryClient.invalidateQueries({ queryKey: ['exploration-sessions', projectId] });
        }}
        onError={setError} />
    </div>
  );
}

function SessionDetail({ projectId, session }: { projectId: string; session: ExplorationSession }) {
  const [selectedPage, setSelectedPage] = useState<DiscoveredPage | null>(null);
  const isLive = session.status === 'Running' || session.status === 'Pending';
  const { frame, status: liveStatus, connected } = useExplorationStream(session.id, isLive);

  const { data: pages = [] } = useQuery({
    queryKey: ['discovered-pages', session.id],
    queryFn: () => listDiscoveredPages(projectId, session.id),
    refetchInterval: session.status === 'Running' ? 5000 : false,
  });

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-6 p-3 rounded-lg bg-gray-50 border border-border">
        <div><p className="text-xs text-muted-foreground">Status</p><Badge variant={statusVariant[session.status] as any}>{session.status}</Badge></div>
        <div><p className="text-xs text-muted-foreground">Pages</p><p className="text-sm font-bold">{liveStatus?.pagesDiscovered ?? session.pagesDiscovered}</p></div>
        <div><p className="text-xs text-muted-foreground">Elements</p><p className="text-sm font-bold">{liveStatus?.elementsDiscovered ?? session.elementsDiscovered}</p></div>
        {session.errorMessage && <p className="text-xs text-red-500">{session.errorMessage}</p>}
        {session.status === 'Running' && (
          <div className="flex-1 h-1.5 rounded-full bg-gray-200 overflow-hidden">
            <div className="h-full w-1/3 bg-violet-500 rounded-full animate-pulse" />
          </div>
        )}
      </div>

      {/* Live browser stream (CDP screencast over SignalR) */}
      {isLive && (
        <Card className="overflow-hidden">
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <span className={cn('h-2 w-2 rounded-full', connected ? 'bg-green-500 animate-pulse' : 'bg-gray-300')} />
                Live Browser
              </CardTitle>
              <span className="text-xs text-muted-foreground truncate max-w-[320px]">
                {liveStatus?.currentUrl ?? (connected ? 'Waiting for first frame…' : 'Connecting…')}
              </span>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <div className="relative bg-slate-900 aspect-video flex items-center justify-center">
              {frame ? (
                <img
                  src={`data:image/jpeg;base64,${frame}`}
                  alt="Live exploration"
                  className="w-full h-full object-contain"
                />
              ) : (
                <div className="flex flex-col items-center gap-2 text-slate-400">
                  <Loader2 className="h-6 w-6 animate-spin" />
                  <p className="text-xs">{connected ? 'Streaming will begin shortly…' : 'Connecting to live stream…'}</p>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Page</TableHead>
            <TableHead>URL</TableHead>
            <TableHead className="text-center">Depth</TableHead>
            <TableHead className="text-center">Elements</TableHead>
            <TableHead className="text-center">Screenshot</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {pages.map((page) => (
            <TableRow key={page.id} className="cursor-pointer" onClick={() => setSelectedPage(page)}>
              <TableCell>
                <div className="font-medium text-sm">{page.name}</div>
                <div className="text-xs text-muted-foreground">{page.path}</div>
              </TableCell>
              <TableCell>
                <div className="flex items-center gap-1">
                  <span className="text-xs text-muted-foreground truncate max-w-[200px]">{page.url}</span>
                  <a href={page.url} target="_blank" rel="noreferrer" onClick={(e) => e.stopPropagation()}>
                    <ExternalLink className="h-3 w-3 text-muted-foreground hover:text-foreground" />
                  </a>
                </div>
              </TableCell>
              <TableCell className="text-center text-sm">{page.depthFromRoot}</TableCell>
              <TableCell className="text-center text-sm">{page.elementCount}</TableCell>
              <TableCell className="text-center">
                {page.screenshotPath && (
                  <img src={fileUrl(page.screenshotPath)} alt={page.name}
                    className="w-20 h-12 object-cover rounded border border-border" />
                )}
              </TableCell>
            </TableRow>
          ))}
          {pages.length === 0 && (
            <TableRow>
              <TableCell colSpan={5} className="text-center py-8 text-muted-foreground text-sm">
                {session.status === 'Pending' || session.status === 'Running' ? 'Exploration in progress…' : 'No pages discovered.'}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      {selectedPage?.screenshotPath && (
        <Card>
          <CardContent className="p-4">
            <p className="text-sm font-semibold mb-2">Screenshot — {selectedPage.name}</p>
            <img src={fileUrl(selectedPage.screenshotPath)} alt={selectedPage.name} className="w-full rounded border border-border" />
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function StartExplorationDialog({ projectId, open, onClose, onStarted, onError }: {
  projectId: string; open: boolean; onClose: () => void;
  onStarted: (sessionId: string) => void; onError: (msg: string) => void;
}) {
  const [environmentId, setEnvironmentId] = useState('');
  const [maxPages, setMaxPages] = useState(20);
  const [maxDepth, setMaxDepth] = useState(3);
  const [seedUrl, setSeedUrl] = useState('');

  const { data: environments = [] } = useQuery({
    queryKey: ['environments', projectId],
    queryFn: () => listEnvironments(projectId),
    enabled: open,
  });

  useEffect(() => {
    if (environments.length > 0 && !environmentId) {
      const def = environments.find((e) => e.isDefault) ?? environments[0];
      setEnvironmentId(def.id);
    }
  }, [environments, environmentId]);

  const mutation = useMutation({
    mutationFn: () => startExploration({ projectId, environmentId, seedUrl: seedUrl.trim() || undefined, maxPages, maxDepth }),
    onSuccess: (session) => onStarted(session.id),
    onError: (e) => onError(getErrorMessage(e)),
  });

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader><DialogTitle>Start application exploration</DialogTitle></DialogHeader>
        <div className="space-y-4 py-1">
          <p className="text-sm text-muted-foreground">
            The Explorer Agent will launch a headless Chromium browser, crawl your application,
            and capture pages, screenshots and UI elements automatically.
          </p>
          <Separator />
          {environments.length === 0 ? (
            <Alert severity="warning" className="text-sm">Add an environment with a Base URL first (Overview tab).</Alert>
          ) : (
            <div className="space-y-1.5">
              <Label>Environment</Label>
              <Select value={environmentId} onValueChange={setEnvironmentId}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {environments.map((env) => (
                    <SelectItem key={env.id} value={env.id}>{env.name} — {env.baseUrl}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
          <div className="space-y-1.5">
            <Label>Override seed URL <span className="text-muted-foreground">(optional)</span></Label>
            <Input placeholder="https://myapp.com/dashboard" value={seedUrl} onChange={(e) => setSeedUrl(e.target.value)} />
            <p className="text-xs text-muted-foreground">Leave blank to use the environment's base URL.</p>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Max pages</Label>
              <Input type="number" min={1} max={200} value={maxPages}
                onChange={(e) => setMaxPages(Math.max(1, Math.min(200, Number(e.target.value))))} />
            </div>
            <div className="space-y-1.5">
              <Label>Max depth</Label>
              <Input type="number" min={1} max={10} value={maxDepth}
                onChange={(e) => setMaxDepth(Math.max(1, Math.min(10, Number(e.target.value))))} />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={!environmentId || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Play className="h-4 w-4 mr-1.5" />}
            {mutation.isPending ? 'Starting…' : 'Start exploration'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
