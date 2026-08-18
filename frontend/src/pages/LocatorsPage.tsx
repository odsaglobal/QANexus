import { useMemo, useState } from 'react';
import { Copy, Plus, Search } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Input } from '../components/ui/input';
import { Badge } from '../components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../components/ui/dialog';
import { Label } from '../components/ui/label';

interface LocatorItem {
  id: string;
  page: string;
  element: string;
  strategy: 'role' | 'label' | 'testid' | 'css' | 'xpath';
  selector: string;
  confidence: number;
  status: 'Healthy' | 'Needs review';
}

const seedLocators: LocatorItem[] = [
  {
    id: 'l1',
    page: '/login',
    element: 'Email input',
    strategy: 'label',
    selector: 'getByLabel("Email")',
    confidence: 0.97,
    status: 'Healthy',
  },
  {
    id: 'l2',
    page: '/login',
    element: 'Sign in button',
    strategy: 'role',
    selector: 'getByRole("button", { name: "Sign in" })',
    confidence: 0.94,
    status: 'Healthy',
  },
  {
    id: 'l3',
    page: '/checkout',
    element: 'Apply coupon',
    strategy: 'css',
    selector: '.coupon-panel button.apply',
    confidence: 0.71,
    status: 'Needs review',
  },
  {
    id: 'l4',
    page: '/projects',
    element: 'New project button',
    strategy: 'role',
    selector: 'getByRole("button", { name: "New project" })',
    confidence: 0.95,
    status: 'Healthy',
  },
];

export function LocatorsPage() {
  const [items, setItems] = useState(seedLocators);
  const [query, setQuery] = useState('');
  const [strategy, setStrategy] = useState<'all' | LocatorItem['strategy']>('all');
  const [open, setOpen] = useState(false);
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [form, setForm] = useState({
    page: '',
    element: '',
    strategy: 'role' as LocatorItem['strategy'],
    selector: '',
  });

  const filtered = useMemo(() => {
    return items.filter((i) => {
      const textOk =
        i.page.toLowerCase().includes(query.toLowerCase()) ||
        i.element.toLowerCase().includes(query.toLowerCase()) ||
        i.selector.toLowerCase().includes(query.toLowerCase());
      const strategyOk = strategy === 'all' || i.strategy === strategy;
      return textOk && strategyOk;
    });
  }, [items, query, strategy]);

  async function copySelector(id: string, selector: string) {
    await navigator.clipboard.writeText(selector);
    setCopiedId(id);
    window.setTimeout(() => setCopiedId(null), 1200);
  }

  function addLocator() {
    const next: LocatorItem = {
      id: `l${Date.now()}`,
      page: form.page,
      element: form.element,
      strategy: form.strategy,
      selector: form.selector,
      confidence: 0.9,
      status: 'Healthy',
    };
    setItems((prev) => [next, ...prev]);
    setOpen(false);
    setForm({ page: '', element: '', strategy: 'role', selector: '' });
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Locator Repository</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Centralized selectors with confidence scoring for stable execution.
          </p>
        </div>
        <Button onClick={() => setOpen(true)}>
          <Plus className="h-4 w-4 mr-1.5" /> Add locator
        </Button>
      </div>

      <div className="flex gap-3">
        <div className="relative max-w-sm w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search page, element, selector..." className="pl-9" />
        </div>
        <select
          value={strategy}
          onChange={(e) => setStrategy(e.target.value as 'all' | LocatorItem['strategy'])}
          className="h-10 rounded-md border border-input bg-background px-3 text-sm"
        >
          <option value="all">All strategies</option>
          <option value="role">role</option>
          <option value="label">label</option>
          <option value="testid">testid</option>
          <option value="css">css</option>
          <option value="xpath">xpath</option>
        </select>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-semibold">{filtered.length} locators</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Page</TableHead>
                <TableHead>Element</TableHead>
                <TableHead>Strategy</TableHead>
                <TableHead>Selector</TableHead>
                <TableHead>Confidence</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="font-medium">{item.page}</TableCell>
                  <TableCell>{item.element}</TableCell>
                  <TableCell>
                    <Badge variant="outline">{item.strategy}</Badge>
                  </TableCell>
                  <TableCell className="max-w-sm truncate text-xs text-muted-foreground">{item.selector}</TableCell>
                  <TableCell>{Math.round(item.confidence * 100)}%</TableCell>
                  <TableCell>
                    <Badge variant={item.status === 'Healthy' ? 'success' : 'warning'}>{item.status}</Badge>
                  </TableCell>
                  <TableCell>
                    <Button variant="ghost" size="sm" onClick={() => copySelector(item.id, item.selector)}>
                      <Copy className="h-3.5 w-3.5 mr-1" /> {copiedId === item.id ? 'Copied' : 'Copy'}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Add locator</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-1">
            <div className="space-y-1.5">
              <Label>Page path</Label>
              <Input value={form.page} onChange={(e) => setForm((p) => ({ ...p, page: e.target.value }))} placeholder="/checkout" />
            </div>
            <div className="space-y-1.5">
              <Label>Element name</Label>
              <Input value={form.element} onChange={(e) => setForm((p) => ({ ...p, element: e.target.value }))} placeholder="Apply coupon" />
            </div>
            <div className="space-y-1.5">
              <Label>Strategy</Label>
              <select
                value={form.strategy}
                onChange={(e) => setForm((p) => ({ ...p, strategy: e.target.value as LocatorItem['strategy'] }))}
                className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
              >
                <option value="role">role</option>
                <option value="label">label</option>
                <option value="testid">testid</option>
                <option value="css">css</option>
                <option value="xpath">xpath</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <Label>Selector</Label>
              <Input value={form.selector} onChange={(e) => setForm((p) => ({ ...p, selector: e.target.value }))} placeholder="getByRole(...)" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button
              onClick={addLocator}
              disabled={!form.page || !form.element || !form.selector}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
