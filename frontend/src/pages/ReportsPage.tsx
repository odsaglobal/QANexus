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
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Alert } from '../components/ui/alert';
import { getReportsSummary } from '../api/reports';

const typeColor: Record<string, string> = {
  Positive: '#22c55e', Negative: '#ef4444', Boundary: '#f59e0b', Smoke: '#a78bfa',
  Security: '#ec4899', Regression: '#14b8a6', Accessibility: '#8b5cf6', Api: '#0ea5e9',
};
const fallbackColors = ['#4f46e5', '#22c55e', '#f59e0b', '#ef4444', '#0ea5e9', '#a78bfa', '#ec4899'];

export function ReportsPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['reports-summary'],
    queryFn: getReportsSummary,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground text-sm">
        <Loader2 className="h-5 w-5 animate-spin mr-2" /> Loading reports…
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-bold text-foreground">Reports</h1>
        <Alert severity="error" className="text-sm">{(error as Error)?.message ?? 'Failed to load reports.'}</Alert>
      </div>
    );
  }

  const totalSteps = data.stepsPassed + data.stepsHealed + data.stepsFailed;
  const mix = data.scenarioMix.map((m, i) => ({
    name: m.type,
    value: m.count,
    color: typeColor[m.type] ?? fallbackColors[i % fallbackColors.length],
  }));
  const mixTotal = mix.reduce((a, b) => a + b.value, 0);
  const maxFailing = Math.max(1, ...data.topFailingScenarios.map((d) => d.failedSteps));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">Reports</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Live quality metrics computed from your real scenario runs and step results.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Step pass rate</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold">{data.stepPassRate}%</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Total runs</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold">{data.totalRuns}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Steps executed</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold">{totalSteps}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Failed steps</CardTitle></CardHeader>
          <CardContent className="text-3xl font-bold text-red-600">{data.stepsFailed}</CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle className="text-sm font-semibold">Weekly execution trend</CardTitle>
          </CardHeader>
          <CardContent>
            {totalSteps === 0 ? (
              <p className="py-16 text-center text-sm text-muted-foreground">No step results yet. Run a scenario to see the trend.</p>
            ) : (
              <ResponsiveContainer width="100%" height={260}>
                <BarChart data={data.weeklyTrend}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#edf2f7" />
                  <XAxis dataKey="week" />
                  <YAxis allowDecimals={false} />
                  <Tooltip />
                  <Bar dataKey="passed" name="Passed" fill="#22c55e" radius={[6, 6, 0, 0]} />
                  <Bar dataKey="failed" name="Failed" fill="#ef4444" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold">Scenario mix</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-center">
            {mix.length === 0 ? (
              <p className="py-16 text-center text-sm text-muted-foreground">No scenarios yet.</p>
            ) : (
              <>
                <ResponsiveContainer width="100%" height={230}>
                  <PieChart>
                    <Pie data={mix} dataKey="value" innerRadius={55} outerRadius={85} stroke="none">
                      {mix.map((item) => <Cell key={item.name} fill={item.color} />)}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
                <div className="w-full space-y-1 text-xs">
                  {mix.map((item) => (
                    <div className="flex items-center justify-between" key={item.name}>
                      <div className="flex items-center gap-2">
                        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: item.color }} />
                        <span>{item.name}</span>
                      </div>
                      <span className="text-muted-foreground">
                        {item.value}{mixTotal > 0 ? ` · ${Math.round((item.value / mixTotal) * 100)}%` : ''}
                      </span>
                    </div>
                  ))}
                </div>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Top failing scenarios</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {data.topFailingScenarios.length === 0 ? (
            <p className="text-sm text-muted-foreground">No failing steps recorded. 🎉</p>
          ) : (
            data.topFailingScenarios.map((d) => (
              <div key={d.title}>
                <div className="mb-1 flex items-center justify-between text-sm">
                  <span className="font-medium truncate mr-2">{d.title}</span>
                  <span className="text-muted-foreground shrink-0">{d.failedSteps} failed step{d.failedSteps === 1 ? '' : 's'}</span>
                </div>
                <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
                  <div className="h-full rounded-full bg-red-500" style={{ width: `${(d.failedSteps / maxFailing) * 100}%` }} />
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
