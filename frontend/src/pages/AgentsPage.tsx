import { useMemo, useState } from 'react';
import { Activity, Bot } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Switch } from '../components/ui/switch';

interface AgentItem {
  id: string;
  name: string;
  mode: 'Autonomous' | 'On-demand';
  enabled: boolean;
  health: 'Healthy' | 'Warning' | 'Stopped';
  throughputPerHour: number;
}

const seedAgents: AgentItem[] = [
  { id: 'a1', name: 'Requirement Intelligence', mode: 'Autonomous', enabled: true, health: 'Healthy', throughputPerHour: 22 },
  { id: 'a2', name: 'Scenario Generator', mode: 'Autonomous', enabled: true, health: 'Healthy', throughputPerHour: 31 },
  { id: 'a3', name: 'Explorer Agent', mode: 'On-demand', enabled: true, health: 'Warning', throughputPerHour: 14 },
  { id: 'a4', name: 'Self-Healing Agent', mode: 'Autonomous', enabled: false, health: 'Stopped', throughputPerHour: 0 },
];

export function AgentsPage() {
  const [agents, setAgents] = useState(seedAgents);

  const totals = useMemo(() => {
    const running = agents.filter((a) => a.enabled).length;
    const healthy = agents.filter((a) => a.health === 'Healthy').length;
    const throughput = agents.reduce((sum, a) => sum + a.throughputPerHour, 0);
    return { running, healthy, throughput };
  }, [agents]);

  function toggle(id: string, enabled: boolean) {
    setAgents((prev) => prev.map((a) => {
      if (a.id !== id) return a;
      return {
        ...a,
        enabled,
        health: enabled ? (a.health === 'Stopped' ? 'Healthy' : a.health) : 'Stopped',
        throughputPerHour: enabled ? (a.throughputPerHour || 10) : 0,
      };
    }));
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">Agent Monitor</h1>
        <p className="text-sm text-muted-foreground mt-0.5">Observe health, throughput and runtime state of AI agents.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Running agents</CardTitle></CardHeader>
          <CardContent className="text-2xl font-bold">{totals.running}/{agents.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Healthy agents</CardTitle></CardHeader>
          <CardContent className="text-2xl font-bold">{totals.healthy}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1"><CardTitle className="text-sm">Total throughput</CardTitle></CardHeader>
          <CardContent className="text-2xl font-bold">{totals.throughput}/hr</CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {agents.map((agent) => (
          <Card key={agent.id}>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base flex items-center gap-2">
                  <Bot className="h-4 w-4 text-violet-600" />
                  {agent.name}
                </CardTitle>
                <Switch checked={agent.enabled} onCheckedChange={(v) => toggle(agent.id, v)} />
              </div>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Mode</span>
                <Badge variant="outline">{agent.mode}</Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Health</span>
                <Badge variant={agent.health === 'Healthy' ? 'success' : agent.health === 'Warning' ? 'warning' : 'secondary'}>
                  {agent.health}
                </Badge>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Throughput</span>
                <span className="font-medium flex items-center gap-1"><Activity className="h-3.5 w-3.5 text-violet-600" /> {agent.throughputPerHour}/hr</span>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
