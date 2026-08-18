import { useMemo, useState } from 'react';
import { Database, Plus, Search } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Input } from '../components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';

type DataStatus = 'Ready' | 'Generating' | 'Expired';

interface DataSet {
  id: string;
  name: string;
  domain: string;
  rows: number;
  updatedAt: string;
  status: DataStatus;
}

const seedData: DataSet[] = [
  { id: 'TD-101', name: 'Checkout users', domain: 'E-Commerce', rows: 250, updatedAt: '2026-07-24 17:20', status: 'Ready' },
  { id: 'TD-102', name: 'Fraud cards', domain: 'Payments', rows: 75, updatedAt: '2026-07-24 15:08', status: 'Ready' },
  { id: 'TD-103', name: 'Address edge cases', domain: 'Marketplace', rows: 120, updatedAt: '2026-07-22 11:37', status: 'Expired' },
];

function statusVariant(status: DataStatus): 'success' | 'warning' | 'secondary' {
  if (status === 'Ready') return 'success';
  if (status === 'Generating') return 'warning';
  return 'secondary';
}

export function TestDataPage() {
  const [items, setItems] = useState(seedData);
  const [query, setQuery] = useState('');

  const filtered = useMemo(
    () => items.filter((i) =>
      i.name.toLowerCase().includes(query.toLowerCase()) ||
      i.domain.toLowerCase().includes(query.toLowerCase()) ||
      i.id.toLowerCase().includes(query.toLowerCase())),
    [items, query],
  );

  function generateDataset() {
    const id = `TD-${Math.floor(200 + Math.random() * 800)}`;
    setItems((prev) => [
      {
        id,
        name: 'Generated synthetic set',
        domain: 'General',
        rows: 100,
        updatedAt: new Date().toISOString().slice(0, 16).replace('T', ' '),
        status: 'Generating',
      },
      ...prev,
    ]);
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Test Data</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Manage reusable synthetic and masked datasets for execution flows.</p>
        </div>
        <Button onClick={generateDataset}><Plus className="h-4 w-4 mr-1.5" /> Generate dataset</Button>
      </div>

      <Card>
        <CardHeader className="pb-2 flex flex-row items-center justify-between">
          <CardTitle className="text-sm font-semibold flex items-center gap-2"><Database className="h-4 w-4 text-violet-600" /> Dataset catalog</CardTitle>
          <div className="relative w-full max-w-sm">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search datasets..." className="pl-9" />
          </div>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Domain</TableHead>
                <TableHead>Rows</TableHead>
                <TableHead>Updated</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="font-medium">{item.id}</TableCell>
                  <TableCell>{item.name}</TableCell>
                  <TableCell>{item.domain}</TableCell>
                  <TableCell>{item.rows}</TableCell>
                  <TableCell>{item.updatedAt}</TableCell>
                  <TableCell><Badge variant={statusVariant(item.status)}>{item.status}</Badge></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
