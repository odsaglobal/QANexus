import {
  BarChart,
  Bar,
  CartesianGrid,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  Cell,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';

const weekly = [
  { week: 'W1', Passed: 120, Failed: 24 },
  { week: 'W2', Passed: 138, Failed: 18 },
  { week: 'W3', Passed: 151, Failed: 21 },
  { week: 'W4', Passed: 166, Failed: 17 },
];

const byType = [
  { name: 'Functional', value: 48, color: '#4f46e5' },
  { name: 'Regression', value: 31, color: '#22c55e' },
  { name: 'Integration', value: 14, color: '#f59e0b' },
  { name: 'Security', value: 7, color: '#ef4444' },
];

const topDefects = [
  { module: 'Checkout', defects: 21 },
  { module: 'User Profile', defects: 13 },
  { module: 'Catalog', defects: 9 },
  { module: 'Search', defects: 8 },
];

export function ReportsPage() {
  const passRate = Math.round((weekly.reduce((a, b) => a + b.Passed, 0) /
    (weekly.reduce((a, b) => a + b.Passed + b.Failed, 0))) * 100);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">Reports</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Snapshot of quality metrics across the latest execution cycles.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Overall pass rate</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold">{passRate}%</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Total executed</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold">{weekly.reduce((a, b) => a + b.Passed + b.Failed, 0)}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Open defects</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold">51</CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle className="text-sm font-semibold">Weekly execution trend</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={260}>
              <BarChart data={weekly}>
                <CartesianGrid strokeDasharray="3 3" stroke="#edf2f7" />
                <XAxis dataKey="week" />
                <YAxis />
                <Tooltip />
                <Bar dataKey="Passed" fill="#22c55e" radius={[6, 6, 0, 0]} />
                <Bar dataKey="Failed" fill="#ef4444" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold">Scenario mix</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-center">
            <ResponsiveContainer width="100%" height={230}>
              <PieChart>
                <Pie data={byType} dataKey="value" innerRadius={55} outerRadius={85} stroke="none">
                  {byType.map((item) => <Cell key={item.name} fill={item.color} />)}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
            <div className="w-full space-y-1 text-xs">
              {byType.map((item) => (
                <div className="flex items-center justify-between" key={item.name}>
                  <div className="flex items-center gap-2">
                    <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: item.color }} />
                    <span>{item.name}</span>
                  </div>
                  <span className="text-muted-foreground">{item.value}%</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Top defect-prone modules</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {topDefects.map((d) => (
            <div key={d.module}>
              <div className="mb-1 flex items-center justify-between text-sm">
                <span className="font-medium">{d.module}</span>
                <span className="text-muted-foreground">{d.defects} defects</span>
              </div>
              <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
                <div className="h-full rounded-full bg-violet-500" style={{ width: `${(d.defects / 21) * 100}%` }} />
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
