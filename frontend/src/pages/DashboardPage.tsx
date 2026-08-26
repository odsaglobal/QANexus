import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
  PieChart, Pie, Cell, RadialBarChart, RadialBar,
} from 'recharts';
import {
  FolderOpen, FlaskConical, Play, TrendingUp, XCircle, FileSearch,
  RefreshCw, AlertCircle, Sparkles, Activity, Loader2,
} from 'lucide-react';
import { getDashboardSummary } from '../api/dashboard';
import { getReportsSummary } from '../api/reports';
import { useAuthStore } from '../store/authStore';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';

// ── Helpers ───────────────────────────────────────────────────────────────────

function StatCard({ label, value, icon, iconBg }: {
  label: string; value: string | number; icon: React.ReactNode; iconBg: string;
}) {
  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-sm text-muted-foreground mb-1">{label}</p>
            <p className="text-3xl font-bold text-foreground">{value}</p>
          </div>
          <div className={`h-11 w-11 rounded-xl flex items-center justify-center ${iconBg}`}>{icon}</div>
        </div>
      </CardContent>
    </Card>
  );
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
export function DashboardPage() {
  const user = useAuthStore((s) => s.user);

  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: getDashboardSummary,
  });
  const { data: reports, isLoading: reportsLoading } = useQuery({
    queryKey: ['reports-summary'],
    queryFn: getReportsSummary,
    refetchInterval: (query) => {
      const running = query.state.data?.runningRuns ?? 0;
      return running > 0 ? 4000 : false;
    },
  });

  const scenarioTotal = summary?.scenarioCount ?? 0;
  const scenariosBySource = [
    { name: 'AI generated', value: summary?.aiScenarioCount ?? 0, color: '#7c3aed' },
    { name: 'Manual', value: summary?.manualScenarioCount ?? 0, color: '#0ea5e9' },
    { name: 'TestRail', value: summary?.testRailScenarioCount ?? 0, color: '#22c55e' },
  ];

  const totalRuns = reports?.totalRuns ?? 0;
  const passRate = reports?.stepPassRate ?? 0;
  const passRateData = [{ value: passRate, fill: passRate >= 80 ? '#22c55e' : passRate >= 50 ? '#f59e0b' : '#ef4444' }];

  const execStatus = useMemo(() => {
    if (!reports) return [];
    const other = Math.max(0, reports.totalRuns - reports.completedRuns - reports.failedRuns - reports.runningRuns);
    return [
      { name: 'Completed', value: reports.completedRuns, color: '#22c55e' },
      { name: 'Failed', value: reports.failedRuns, color: '#ef4444' },
      { name: 'Running', value: reports.runningRuns, color: '#3b82f6' },
      { name: 'Other', value: other, color: '#94a3b8' },
    ].filter((e) => e.value > 0);
  }, [reports]);

  const stepOutcomes = useMemo(() => {
    if (!reports) return [];
    return [
      { name: 'Passed', value: reports.stepsPassed, color: '#22c55e' },
      { name: 'Healed', value: reports.stepsHealed, color: '#f59e0b' },
      { name: 'Failed', value: reports.stepsFailed, color: '#ef4444' },
    ].filter((e) => e.value > 0);
  }, [reports]);
  const stepTotal = (reports?.stepsPassed ?? 0) + (reports?.stepsHealed ?? 0) + (reports?.stepsFailed ?? 0);

  const insights = useMemo(() => {
    const out: { icon: React.ElementType; color: string; label: string; desc: string }[] = [];
    if (!reports) return out;
    if (reports.runningRuns > 0) {
      out.push({ icon: Activity, color: 'text-blue-600 bg-blue-50', label: `${reports.runningRuns} run(s) in progress`, desc: 'Live executions are running right now.' });
    }
    if (reports.stepsHealed > 0) {
      out.push({ icon: RefreshCw, color: 'text-amber-600 bg-amber-50', label: `${reports.stepsHealed} step(s) auto-healed`, desc: 'The self-healing agent recovered locators during runs.' });
    }
    if ((summary?.aiScenarioCount ?? 0) > 0) {
      out.push({ icon: Sparkles, color: 'text-violet-600 bg-violet-50', label: `${summary?.aiScenarioCount} AI-generated scenarios`, desc: 'Scenarios authored by the generation agent.' });
    }
    if (reports.topFailingScenarios.length > 0) {
      const t = reports.topFailingScenarios[0];
      out.push({ icon: AlertCircle, color: 'text-red-600 bg-red-50', label: `"${t.title}" needs review`, desc: `${t.failedSteps} failed step(s) recorded.` });
    }
    if (stepTotal > 0) {
      out.push({ icon: TrendingUp, color: 'text-green-600 bg-green-50', label: `Step pass rate ${reports.stepPassRate}%`, desc: `${stepTotal} steps executed across all runs.` });
    }
    return out.slice(0, 4);
  }, [reports, summary, stepTotal]);

  const loading = summaryLoading || reportsLoading;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Dashboard</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            Welcome back, {user?.displayName?.split(' ')[0] ?? 'there'}! Here's what's happening with your projects.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" asChild>
            <Link to="/reports"><TrendingUp className="h-4 w-4 mr-1.5" /> View Reports</Link>
          </Button>
          <Button size="sm" asChild>
            <Link to="/projects"><FolderOpen className="h-4 w-4 mr-1.5" /> Projects</Link>
          </Button>
        </div>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-4">
        <StatCard label="Projects" value={summary?.projectCount ?? 0} icon={<FolderOpen className="h-5 w-5 text-blue-600" />} iconBg="bg-blue-100" />
        <StatCard label="Scenarios" value={scenarioTotal.toLocaleString()} icon={<FlaskConical className="h-5 w-5 text-green-600" />} iconBg="bg-green-100" />
        <StatCard label="Executions" value={totalRuns.toLocaleString()} icon={<Play className="h-5 w-5 text-violet-600" />} iconBg="bg-violet-100" />
        <StatCard label="Pass Rate" value={`${passRate}%`} icon={<TrendingUp className="h-5 w-5 text-teal-600" />} iconBg="bg-teal-100" />
        <StatCard label="Failed Steps" value={reports?.stepsFailed ?? 0} icon={<XCircle className="h-5 w-5 text-red-500" />} iconBg="bg-red-100" />
        <StatCard label="Pages Discovered" value={summary?.discoveredPageCount ?? 0} icon={<FileSearch className="h-5 w-5 text-orange-500" />} iconBg="bg-orange-100" />
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 xl:grid-cols-5 gap-4">
        <Card className="xl:col-span-3">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-semibold">Execution Trend <span className="font-normal text-muted-foreground">(last 6 weeks · steps)</span></CardTitle>
          </CardHeader>
          <CardContent>
            {stepTotal === 0 ? (
              <div className="h-[200px] flex items-center justify-center text-sm text-muted-foreground">
                {loading ? <Loader2 className="h-5 w-5 animate-spin" /> : 'No runs yet. Execute a scenario to see the trend.'}
              </div>
            ) : (
              <ResponsiveContainer width="100%" height={200}>
                <LineChart data={reports?.weeklyTrend} margin={{ top: 0, right: 10, left: -20, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                  <XAxis dataKey="week" tick={{ fontSize: 11, fill: '#94a3b8' }} axisLine={false} tickLine={false} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={{ fontSize: 12, borderRadius: 8, border: '1px solid #e2e8f0' }} />
                  <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: 11 }} />
                  <Line type="monotone" dataKey="passed" name="Passed" stroke="#22c55e" strokeWidth={2} dot={false} />
                  <Line type="monotone" dataKey="failed" name="Failed" stroke="#ef4444" strokeWidth={2} dot={false} />
                </LineChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>

        {/* Pass Rate */}
        <Card className="xl:col-span-1 flex flex-col items-center justify-center">
          <CardHeader className="pb-0 w-full">
            <CardTitle className="text-sm font-semibold">Step Pass Rate</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-center pt-2 flex-1">
            <div className="relative">
              <ResponsiveContainer width={140} height={140}>
                <RadialBarChart cx="50%" cy="50%" innerRadius="65%" outerRadius="90%"
                  data={passRateData} startAngle={220} endAngle={-40}>
                  <RadialBar dataKey="value" cornerRadius={6} background={{ fill: '#f1f5f9' }} />
                </RadialBarChart>
              </ResponsiveContainer>
              <div className="absolute inset-0 flex flex-col items-center justify-center">
                <span className="text-2xl font-bold text-foreground">{passRate}%</span>
                <span className="text-xs text-muted-foreground">Overall</span>
              </div>
            </div>
            <p className="text-xs text-muted-foreground mt-1">{stepTotal.toLocaleString()} steps executed</p>
          </CardContent>
        </Card>

        {/* Execution Status */}
        <Card className="xl:col-span-1">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-semibold">Execution Status</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-center pt-0">
            {execStatus.length === 0 ? (
              <div className="h-[120px] flex items-center justify-center text-xs text-muted-foreground">No runs yet</div>
            ) : (
              <>
                <div className="relative">
                  <ResponsiveContainer width={120} height={120}>
                    <PieChart>
                      <Pie data={execStatus} cx="50%" cy="50%" innerRadius={35} outerRadius={55} dataKey="value" stroke="none">
                        {execStatus.map((e, i) => <Cell key={i} fill={e.color} />)}
                      </Pie>
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="absolute inset-0 flex flex-col items-center justify-center">
                    <span className="text-lg font-bold">{totalRuns}</span>
                    <span className="text-[10px] text-muted-foreground">Total</span>
                  </div>
                </div>
                <div className="w-full mt-3 space-y-1.5">
                  {execStatus.map((e) => (
                    <div key={e.name} className="flex items-center justify-between text-xs">
                      <div className="flex items-center gap-1.5">
                        <span className="h-2 w-2 rounded-full flex-shrink-0" style={{ background: e.color }} />
                        <span className="text-muted-foreground">{e.name}</span>
                      </div>
                      <span className="font-semibold">{e.value} ({totalRuns ? ((e.value / totalRuns) * 100).toFixed(0) : 0}%)</span>
                    </div>
                  ))}
                </div>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      {/* AI Insights */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-semibold">AI Insights</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {insights.length === 0 ? (
            <p className="text-sm text-muted-foreground">Run scenarios to surface insights.</p>
          ) : insights.map((ins, i) => (
            <div key={i} className="flex items-start gap-3">
              <div className={`h-8 w-8 rounded-lg flex items-center justify-center flex-shrink-0 ${ins.color}`}>
                <ins.icon className="h-4 w-4" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-foreground leading-tight">{ins.label}</p>
                <p className="text-xs text-muted-foreground mt-0.5">{ins.desc}</p>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Bottom row */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        {/* Scenarios by Source */}
        <Card>
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold">Scenarios by Source</CardTitle>
              <span className="text-xs text-muted-foreground">{scenarioTotal.toLocaleString()} total</span>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {scenarioTotal === 0 && (
              <p className="text-sm text-muted-foreground">No scenarios yet. Generate, import or sync from a project.</p>
            )}
            {scenarioTotal > 0 && scenariosBySource.map((s) => (
              <div key={s.name}>
                <div className="flex items-center justify-between mb-1">
                  <span className="text-sm text-foreground truncate mr-2">{s.name}</span>
                  <span className="text-sm font-bold text-foreground flex-shrink-0">{s.value}</span>
                </div>
                <div className="h-1.5 rounded-full bg-gray-100 overflow-hidden">
                  <div className="h-full rounded-full" style={{ width: `${scenarioTotal ? (s.value / scenarioTotal) * 100 : 0}%`, background: s.color }} />
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        {/* Step Outcomes */}
        <Card className="flex flex-col items-center">
          <CardHeader className="pb-0 w-full">
            <CardTitle className="text-sm font-semibold">Step Outcomes</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-center pt-2 flex-1 w-full">
            {stepOutcomes.length === 0 ? (
              <div className="h-[130px] flex items-center justify-center text-xs text-muted-foreground">No steps executed yet</div>
            ) : (
              <>
                <div className="relative">
                  <ResponsiveContainer width={130} height={130}>
                    <PieChart>
                      <Pie data={stepOutcomes} cx="50%" cy="50%" innerRadius={38} outerRadius={58} dataKey="value" stroke="none">
                        {stepOutcomes.map((e, i) => <Cell key={i} fill={e.color} />)}
                      </Pie>
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="absolute inset-0 flex flex-col items-center justify-center">
                    <span className="text-xl font-bold">{stepTotal}</span>
                    <span className="text-[10px] text-muted-foreground">Steps</span>
                  </div>
                </div>
                <div className="w-full mt-3 space-y-1.5">
                  {stepOutcomes.map((e) => (
                    <div key={e.name} className="flex items-center justify-between text-xs">
                      <div className="flex items-center gap-1.5">
                        <span className="h-2 w-2 rounded-full" style={{ background: e.color }} />
                        <span className="text-muted-foreground">{e.name}</span>
                      </div>
                      <span className="font-semibold">{e.value.toLocaleString()}</span>
                    </div>
                  ))}
                </div>
              </>
            )}
          </CardContent>
        </Card>

        {/* Platform Activity */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-semibold">Platform Activity</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2.5">
            {[
              { label: 'Explorations run', value: summary?.explorationSessionCount ?? 0 },
              { label: 'Pages discovered', value: summary?.discoveredPageCount ?? 0 },
              { label: 'Business docs', value: summary?.requirementCount ?? 0 },
              { label: 'Features', value: summary?.featureCount ?? 0 },
              { label: 'Running now', value: reports?.runningRuns ?? 0 },
            ].map((row) => (
              <div key={row.label} className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">{row.label}</span>
                <span className="text-sm font-semibold text-foreground">{row.value.toLocaleString()}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
