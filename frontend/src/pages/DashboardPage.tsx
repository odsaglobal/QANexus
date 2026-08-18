import { useQuery } from '@tanstack/react-query';
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
  PieChart, Pie, Cell, RadialBarChart, RadialBar,
} from 'recharts';
import {
  FolderOpen, FlaskConical, Play, TrendingUp, Bug, Bot,
  ArrowUpRight, ArrowDownRight, BarChart2, RefreshCw, AlertCircle,
} from 'lucide-react';
import { listProjects } from '../api/projects';
import { getDashboardSummary } from '../api/dashboard';
import { useAuthStore } from '../store/authStore';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';

// ── Mock data matching the screenshot ────────────────────────────────────────
const execTrend = [
  { date: 'May 15', Passed: 120, Failed: 45, Blocked: 18 },
  { date: 'May 16', Passed: 145, Failed: 52, Blocked: 22 },
  { date: 'May 17', Passed: 130, Failed: 38, Blocked: 15 },
  { date: 'May 18', Passed: 160, Failed: 60, Blocked: 25 },
  { date: 'May 19', Passed: 155, Failed: 42, Blocked: 20 },
  { date: 'May 20', Passed: 175, Failed: 55, Blocked: 28 },
  { date: 'May 21', Passed: 185, Failed: 48, Blocked: 22 },
];

const execStatus = [
  { name: 'Passed',      value: 529, color: '#22c55e' },
  { name: 'Failed',      value: 32,  color: '#ef4444' },
  { name: 'Blocked',     value: 7,   color: '#f59e0b' },
  { name: 'In Progress', value: 5,   color: '#3b82f6' },
];

const recentExecutions = [
  { name: 'Checkout Flow Test',    project: 'E-Commerce Platform', env: 'QA', status: 'Passed',  time: 'May 21, 2025 10:30 AM', duration: '00:04:32' },
  { name: 'User Registration Test',project: 'E-Commerce Platform', env: 'QA', status: 'Passed',  time: 'May 21, 2025 10:20 AM', duration: '00:02:18' },
  { name: 'Payment Gateway Test',  project: 'E-Commerce Platform', env: 'QA', status: 'Failed',  time: 'May 21, 2025 10:10 AM', duration: '00:06:45' },
  { name: 'Search Functionality',  project: 'E-Commerce Platform', env: 'QA', status: 'Passed',  time: 'May 21, 2025 10:00 AM', duration: '00:01:58' },
  { name: 'Add to Cart Test',      project: 'E-Commerce Platform', env: 'QA', status: 'Passed',  time: 'May 21, 2025 09:50 AM', duration: '00:01:32' },
];

const aiRecommendations = [
  { icon: TrendingUp, color: 'text-green-600 bg-green-50', label: 'Pass rate improved by 5.35%', desc: 'Good job! Pass rate improved in the last 7 days.', time: '2h ago' },
  { icon: AlertCircle, color: 'text-violet-600 bg-violet-50', label: 'Flaky tests detected', desc: '12 flaky tests found in 3 projects.', time: '4h ago' },
  { icon: RefreshCw, color: 'text-amber-600 bg-amber-50', label: 'Locators updated', desc: 'AI healed 28 broken locators automatically.', time: '6h ago' },
  { icon: FlaskConical, color: 'text-blue-600 bg-blue-50', label: 'New scenarios generated', desc: 'AI generated 45 new scenarios from requirements.', time: '8h ago' },
];

const reqCoverage = [
  { name: 'Covered',          value: 1240, color: '#22c55e' },
  { name: 'Partially Covered',value: 280,  color: '#f59e0b' },
  { name: 'Not Covered',      value: 80,   color: '#ef4444' },
];

const activeAgents = [
  { name: 'Requirement Intelligence Agent', status: 'Running' },
  { name: 'Scenario Generation Agent',      status: 'Running' },
  { name: 'Application Explorer Agent',     status: 'Running' },
  { name: 'Self Healing Agent',             status: 'Idle' },
  { name: 'Failure Analysis Agent',         status: 'Running' },
];

const passRateData = [{ value: 92.35, fill: '#22c55e' }];

// ── Helpers ───────────────────────────────────────────────────────────────────
function StatusBadge({ status }: { status: string }) {
  const map: Record<string, string> = {
    Passed:  'bg-green-100 text-green-700',
    Failed:  'bg-red-100 text-red-700',
    Blocked: 'bg-amber-100 text-amber-700',
    Running: 'bg-green-100 text-green-700',
    Idle:    'bg-gray-100 text-gray-600',
  };
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${map[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {status}
    </span>
  );
}

interface StatCardProps {
  label: string;
  value: string | number;
  icon: React.ReactNode;
  iconBg: string;
  trend?: { value: string; up: boolean };
}

function StatCard({ label, value, icon, iconBg, trend }: StatCardProps) {
  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-sm text-muted-foreground mb-1">{label}</p>
            <p className="text-3xl font-bold text-foreground">{value}</p>
            {trend && (
              <div className={`flex items-center gap-1 mt-1.5 text-xs font-medium ${trend.up ? 'text-green-600' : 'text-red-500'}`}>
                {trend.up ? <ArrowUpRight className="h-3 w-3" /> : <ArrowDownRight className="h-3 w-3" />}
                {trend.value} vs last month
              </div>
            )}
          </div>
          <div className={`h-11 w-11 rounded-xl flex items-center justify-center ${iconBg}`}>
            {icon}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const { data } = useQuery({
    queryKey: ['projects'],
    queryFn: () => listProjects({ page: 1, pageSize: 50 }),
  });
  const { data: summary } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: getDashboardSummary,
  });
  const projectCount = summary?.projectCount ?? data?.items.length ?? 0;

  const scenariosBySource = [
    { name: 'AI generated', value: summary?.aiScenarioCount ?? 0, color: '#7c3aed' },
    { name: 'Manual', value: summary?.manualScenarioCount ?? 0, color: '#0ea5e9' },
    { name: 'TestRail', value: summary?.testRailScenarioCount ?? 0, color: '#22c55e' },
  ];
  const scenarioTotal = summary?.scenarioCount ?? 0;

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Dashboard</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            Welcome back, {user?.displayName?.split(' ')[0] ?? 'there'}! Here's what's happening with your projects.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm">
            <BarChart2 className="h-4 w-4 mr-1.5" /> Upload Requirements
          </Button>
          <Button size="sm">
            <FolderOpen className="h-4 w-4 mr-1.5" /> New Project
          </Button>
        </div>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-4">
        <StatCard label="Projects" value={projectCount} icon={<FolderOpen className="h-5 w-5 text-blue-600" />} iconBg="bg-blue-100" trend={{ value: '↑12%', up: true }} />
        <StatCard label="Scenarios" value={scenarioTotal.toLocaleString()} icon={<FlaskConical className="h-5 w-5 text-green-600" />} iconBg="bg-green-100" />
        <StatCard label="Executions" value="573" icon={<Play className="h-5 w-5 text-violet-600" />} iconBg="bg-violet-100" trend={{ value: '↑24%', up: true }} />
        <StatCard label="Pass Rate" value="92.35%" icon={<TrendingUp className="h-5 w-5 text-teal-600" />} iconBg="bg-teal-100" trend={{ value: '↑5.35%', up: true }} />
        <StatCard label="Bugs Found" value="312" icon={<Bug className="h-5 w-5 text-red-500" />} iconBg="bg-red-100" trend={{ value: '↓8%', up: false }} />
        <StatCard label="AI Agents" value="8" icon={<Bot className="h-5 w-5 text-orange-500" />} iconBg="bg-orange-100" />
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 xl:grid-cols-5 gap-4">
        {/* Execution Trend */}
        <Card className="xl:col-span-3">
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold">Execution Trend</CardTitle>
              <select className="text-xs border border-border rounded-md px-2 py-1 text-muted-foreground bg-background">
                <option>Last 7 Days</option>
                <option>Last 30 Days</option>
              </select>
            </div>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={execTrend} margin={{ top: 0, right: 10, left: -20, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="date" tick={{ fontSize: 11, fill: '#94a3b8' }} axisLine={false} tickLine={false} />
                <YAxis tick={{ fontSize: 11, fill: '#94a3b8' }} axisLine={false} tickLine={false} />
                <Tooltip contentStyle={{ fontSize: 12, borderRadius: 8, border: '1px solid #e2e8f0' }} />
                <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: 11 }} />
                <Line type="monotone" dataKey="Passed" stroke="#22c55e" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="Failed" stroke="#ef4444" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="Blocked" stroke="#f59e0b" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        {/* Pass Rate */}
        <Card className="xl:col-span-1 flex flex-col items-center justify-center">
          <CardHeader className="pb-0 w-full">
            <CardTitle className="text-sm font-semibold">Pass Rate</CardTitle>
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
                <span className="text-2xl font-bold text-foreground">92.35%</span>
                <span className="text-xs text-muted-foreground">Overall</span>
              </div>
            </div>
            <p className="text-xs text-green-600 font-medium mt-1">↑ 5.35% vs last 7 days</p>
          </CardContent>
        </Card>

        {/* Execution Status */}
        <Card className="xl:col-span-1">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-semibold">Execution Status</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-center pt-0">
            <div className="relative">
              <ResponsiveContainer width={120} height={120}>
                <PieChart>
                  <Pie data={execStatus} cx="50%" cy="50%" innerRadius={35} outerRadius={55}
                    dataKey="value" stroke="none">
                    {execStatus.map((e, i) => <Cell key={i} fill={e.color} />)}
                  </Pie>
                </PieChart>
              </ResponsiveContainer>
              <div className="absolute inset-0 flex flex-col items-center justify-center">
                <span className="text-lg font-bold">573</span>
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
                  <span className="font-semibold">{e.value} ({((e.value/573)*100).toFixed(1)}%)</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Recent Executions + AI Recommendations */}
      <div className="grid grid-cols-1 xl:grid-cols-5 gap-4">
        {/* Recent Executions */}
        <Card className="xl:col-span-3">
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold">Recent Executions</CardTitle>
              <Button variant="ghost" size="sm" className="text-xs h-7 text-violet-600">View All</Button>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border">
                  {['Execution Name','Project','Environment','Status','Start Time','Duration'].map((h) => (
                    <th key={h} className="px-4 py-2.5 text-left text-xs font-medium text-muted-foreground">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {recentExecutions.map((row, i) => (
                  <tr key={i} className="border-b border-border last:border-0 hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-2.5 font-medium text-sm">{row.name}</td>
                    <td className="px-4 py-2.5 text-muted-foreground text-xs">{row.project}</td>
                    <td className="px-4 py-2.5 text-muted-foreground text-xs">{row.env}</td>
                    <td className="px-4 py-2.5"><StatusBadge status={row.status} /></td>
                    <td className="px-4 py-2.5 text-muted-foreground text-xs">{row.time}</td>
                    <td className="px-4 py-2.5 text-muted-foreground text-xs">{row.duration}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>

        {/* AI Recommendations */}
        <Card className="xl:col-span-2">
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold">AI Recommendations</CardTitle>
              <Button variant="ghost" size="sm" className="text-xs h-7 text-violet-600">View All</Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {aiRecommendations.map((ins, i) => (
              <div key={i} className="flex items-start gap-3">
                <div className={`h-8 w-8 rounded-lg flex items-center justify-center flex-shrink-0 ${ins.color}`}>
                  <ins.icon className="h-4 w-4" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold text-foreground leading-tight">{ins.label}</p>
                  <p className="text-xs text-muted-foreground mt-0.5">{ins.desc}</p>
                </div>
                <span className="text-xs text-muted-foreground flex-shrink-0">{ins.time}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

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

        {/* Requirements Coverage */}
        <Card className="flex flex-col items-center">
          <CardHeader className="pb-0 w-full">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold">Requirements Coverage</CardTitle>
              <Button variant="ghost" size="sm" className="text-xs h-7 text-violet-600">View All</Button>
            </div>
          </CardHeader>
          <CardContent className="flex flex-col items-center pt-2 flex-1">
            <div className="relative">
              <ResponsiveContainer width={130} height={130}>
                <PieChart>
                  <Pie data={reqCoverage} cx="50%" cy="50%" innerRadius={38} outerRadius={58}
                    dataKey="value" stroke="none">
                    {reqCoverage.map((e, i) => <Cell key={i} fill={e.color} />)}
                  </Pie>
                </PieChart>
              </ResponsiveContainer>
              <div className="absolute inset-0 flex flex-col items-center justify-center">
                <span className="text-xl font-bold">78%</span>
                <span className="text-[10px] text-muted-foreground">Covered</span>
              </div>
            </div>
            <div className="w-full mt-3 space-y-1.5">
              {reqCoverage.map((e) => (
                <div key={e.name} className="flex items-center justify-between text-xs">
                  <div className="flex items-center gap-1.5">
                    <span className="h-2 w-2 rounded-full" style={{ background: e.color }} />
                    <span className="text-muted-foreground">{e.name}</span>
                  </div>
                  <span className="font-semibold">{e.value.toLocaleString()}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Active AI Agents */}
        <Card>
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-semibold">Active AI Agents</CardTitle>
              <Button variant="ghost" size="sm" className="text-xs h-7 text-violet-600">View All</Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-2.5">
            {activeAgents.map((a, i) => (
              <div key={i} className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="h-7 w-7 rounded-lg bg-violet-50 flex items-center justify-center">
                    <Bot className="h-3.5 w-3.5 text-violet-600" />
                  </div>
                  <span className="text-sm font-medium text-foreground">{a.name}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className={`h-1.5 w-1.5 rounded-full ${a.status === 'Running' ? 'bg-green-500' : 'bg-gray-400'}`} />
                  <span className={`text-xs font-medium ${a.status === 'Running' ? 'text-green-600' : 'text-gray-500'}`}>{a.status}</span>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
