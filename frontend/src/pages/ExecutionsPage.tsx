import { useMemo, useState } from 'react';
import { Clock3, Play, Search } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Input } from '../components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';

type ExecutionStatus = 'Passed' | 'Failed' | 'Running' | 'Queued';

interface ExecutionItem {
  id: string;
  scenario: string;
  project: string;
  environment: string;
  startedAt: string;
  duration: string;
  status: ExecutionStatus;
}

const seedExecutions: ExecutionItem[] = [
  { id: 'E-3198', scenario: 'Checkout with valid card', project: 'E-Commerce', environment: 'QA', startedAt: '2026-07-25 10:31', duration: '00:04:11', status: 'Passed' },
  { id: 'E-3197', scenario: 'Checkout with invalid card', project: 'E-Commerce', environment: 'QA', startedAt: '2026-07-25 10:20', duration: '00:03:52', status: 'Failed' },
  { id: 'E-3196', scenario: 'User registration', project: 'Marketplace', environment: 'Staging', startedAt: '2026-07-25 10:16', duration: '00:02:48', status: 'Passed' },
  { id: 'E-3195', scenario: 'Product search', project: 'Marketplace', environment: 'QA', startedAt: '2026-07-25 10:10', duration: '00:01:23', status: 'Running' },
  { id: 'E-3194', scenario: 'Profile update', project: 'Banking', environment: 'UAT', startedAt: '2026-07-25 10:00', duration: '--', status: 'Queued' },
];

function statusVariant(status: ExecutionStatus): 'success' | 'destructive' | 'info' | 'secondary' {
  if (status === 'Passed') return 'success';
  if (status === 'Failed') return 'destructive';
  if (status === 'Running') return 'info';
  return 'secondary';
}

export function ExecutionsPage() {
  const [items, setItems] = useState(seedExecutions);
  const [q, setQ] = useState('');
  const [status, setStatus] = useState<'all' | ExecutionStatus>('all');

  const filtered = useMemo(() => {
    return items.filter((i) => {
      const textOk =
        i.id.toLowerCase().includes(q.toLowerCase()) ||
        i.scenario.toLowerCase().includes(q.toLowerCase()) ||
        i.project.toLowerCase().includes(q.toLowerCase());
      const statusOk = status === 'all' || i.status === status;
      return textOk && statusOk;
    });
  }, [items, q, status]);

  function triggerExecution() {
    const id = `E-${Math.floor(3200 + Math.random() * 100)}`;
    setItems((prev) => [
      {
        id,
        scenario: 'Smoke suite (auto-trigger)',
        project: 'E-Commerce',
        environment: 'QA',
        startedAt: new Date().toISOString().slice(0, 16).replace('T', ' '),
        duration: '--',
        status: 'Queued',
      },
      ...prev,
    ]);
  }

  const runningCount = items.filter((i) => i.status === 'Running' || i.status === 'Queued').length;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Executions</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Track scenario runs across projects and environments.</p>
        </div>
        <Button onClick={triggerExecution}>
          <Play className="h-4 w-4 mr-1.5" /> Trigger run
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Total runs</CardTitle></CardHeader>
          <CardContent className="text-2xl font-bold">{items.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Pass rate</CardTitle></CardHeader>
          <CardContent className="text-2xl font-bold">
            {Math.round((items.filter((i) => i.status === 'Passed').length / items.length) * 100)}%
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Active queue</CardTitle></CardHeader>
          <CardContent className="text-2xl font-bold flex items-center gap-2"><Clock3 className="h-5 w-5 text-violet-600" /> {runningCount}</CardContent>
        </Card>
      </div>

      <div className="flex gap-3">
        <div className="relative max-w-sm w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input className="pl-9" placeholder="Search by id, scenario, project..." value={q} onChange={(e) => setQ(e.target.value)} />
        </div>
        <select
          value={status}
          onChange={(e) => setStatus(e.target.value as 'all' | ExecutionStatus)}
          className="h-10 rounded-md border border-input bg-background px-3 text-sm"
        >
          <option value="all">All statuses</option>
          <option value="Passed">Passed</option>
          <option value="Failed">Failed</option>
          <option value="Running">Running</option>
          <option value="Queued">Queued</option>
        </select>
      </div>

      <Card>
        <CardContent className="pt-6">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Scenario</TableHead>
                <TableHead>Project</TableHead>
                <TableHead>Environment</TableHead>
                <TableHead>Started</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="font-medium">{r.id}</TableCell>
                  <TableCell>{r.scenario}</TableCell>
                  <TableCell>{r.project}</TableCell>
                  <TableCell>{r.environment}</TableCell>
                  <TableCell>{r.startedAt}</TableCell>
                  <TableCell>{r.duration}</TableCell>
                  <TableCell><Badge variant={statusVariant(r.status)}>{r.status}</Badge></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
