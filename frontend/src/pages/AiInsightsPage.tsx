import { useMemo, useState } from 'react';
import { AlertTriangle, Bot, Lightbulb, ShieldAlert, Sparkles } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';

interface Insight {
  id: string;
  title: string;
  summary: string;
  severity: 'Info' | 'Warning' | 'Critical';
  category: 'Quality' | 'Security' | 'Reliability';
  suggestedAction: string;
}

const seedInsights: Insight[] = [
  {
    id: 'I-9001',
    title: 'Flaky trend increasing in Checkout module',
    summary: 'Failure oscillation indicates unstable locator and timing race in payment OTP modal.',
    severity: 'Warning',
    category: 'Reliability',
    suggestedAction: 'Enable strict waitFor conditions and regenerate OTP modal locator set.',
  },
  {
    id: 'I-9002',
    title: 'Authorization bypass scenario missing',
    summary: 'No negative tests currently validate role escalation protections on admin APIs.',
    severity: 'Critical',
    category: 'Security',
    suggestedAction: 'Generate RBAC abuse scenarios and gate releases on these checks.',
  },
  {
    id: 'I-9003',
    title: 'Coverage gap in profile validation rules',
    summary: 'Boundary checks for UTF-8 names and phone masks are below target threshold.',
    severity: 'Info',
    category: 'Quality',
    suggestedAction: 'Generate boundary-value set for profile edit flows.',
  },
];

function severityVariant(severity: Insight['severity']): 'info' | 'warning' | 'destructive' {
  if (severity === 'Critical') return 'destructive';
  if (severity === 'Warning') return 'warning';
  return 'info';
}

export function AiInsightsPage() {
  const [filter, setFilter] = useState<'All' | Insight['category']>('All');

  const filtered = useMemo(
    () => seedInsights.filter((i) => filter === 'All' || i.category === filter),
    [filter],
  );

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">AI Insights</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Actionable recommendations generated from recent executions and requirement analysis.</p>
        </div>
        <div className="flex items-center gap-2">
          <button className={`h-9 px-3 rounded-md text-sm border ${filter === 'All' ? 'bg-violet-50 border-violet-200 text-violet-700' : 'border-input'}`} onClick={() => setFilter('All')}>All</button>
          <button className={`h-9 px-3 rounded-md text-sm border ${filter === 'Quality' ? 'bg-violet-50 border-violet-200 text-violet-700' : 'border-input'}`} onClick={() => setFilter('Quality')}>Quality</button>
          <button className={`h-9 px-3 rounded-md text-sm border ${filter === 'Reliability' ? 'bg-violet-50 border-violet-200 text-violet-700' : 'border-input'}`} onClick={() => setFilter('Reliability')}>Reliability</button>
          <button className={`h-9 px-3 rounded-md text-sm border ${filter === 'Security' ? 'bg-violet-50 border-violet-200 text-violet-700' : 'border-input'}`} onClick={() => setFilter('Security')}>Security</button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card><CardContent className="pt-6"><div className="text-sm text-muted-foreground">Open insights</div><div className="text-3xl font-bold mt-1">{seedInsights.length}</div></CardContent></Card>
        <Card><CardContent className="pt-6"><div className="text-sm text-muted-foreground">Critical</div><div className="text-3xl font-bold mt-1">{seedInsights.filter((i) => i.severity === 'Critical').length}</div></CardContent></Card>
        <Card><CardContent className="pt-6"><div className="text-sm text-muted-foreground">AI confidence</div><div className="text-3xl font-bold mt-1">91%</div></CardContent></Card>
      </div>

      <div className="space-y-4">
        {filtered.map((insight) => (
          <Card key={insight.id}>
            <CardHeader className="pb-2">
              <div className="flex items-start justify-between gap-3">
                <CardTitle className="text-base flex items-center gap-2">
                  {insight.category === 'Security' && <ShieldAlert className="h-4 w-4 text-red-500" />}
                  {insight.category === 'Reliability' && <AlertTriangle className="h-4 w-4 text-amber-500" />}
                  {insight.category === 'Quality' && <Lightbulb className="h-4 w-4 text-blue-500" />}
                  {insight.title}
                </CardTitle>
                <div className="flex items-center gap-2">
                  <Badge variant="outline" className="gap-1"><Bot className="h-3 w-3" /> {insight.category}</Badge>
                  <Badge variant={severityVariant(insight.severity)}>{insight.severity}</Badge>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <p className="text-muted-foreground">{insight.summary}</p>
              <div className="rounded-md border border-violet-100 bg-violet-50/60 px-3 py-2 text-violet-900 flex items-start gap-2">
                <Sparkles className="h-4 w-4 mt-0.5 text-violet-600" />
                <p><span className="font-medium">Suggested action:</span> {insight.suggestedAction}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
