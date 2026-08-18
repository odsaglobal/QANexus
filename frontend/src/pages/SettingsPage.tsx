import { useEffect, useMemo, useState } from 'react';
import { Save } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Switch } from '../components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';

interface Preferences {
  orgName: string;
  defaultEnvironment: string;
  notifyOnFailure: boolean;
  autoHealLocators: boolean;
  maxParallelExecutions: number;
}

const storageKey = 'atip.settings.preferences';

const defaultPrefs: Preferences = {
  orgName: 'ATiP Organization',
  defaultEnvironment: 'QA',
  notifyOnFailure: true,
  autoHealLocators: true,
  maxParallelExecutions: 5,
};

export function SettingsPage() {
  const [prefs, setPrefs] = useState<Preferences>(defaultPrefs);
  const [savedAt, setSavedAt] = useState<string | null>(null);

  useEffect(() => {
    const raw = localStorage.getItem(storageKey);
    if (!raw) return;
    try {
      const parsed = JSON.parse(raw) as Preferences;
      setPrefs({ ...defaultPrefs, ...parsed });
    } catch {
      localStorage.removeItem(storageKey);
    }
  }, []);

  function save() {
    localStorage.setItem(storageKey, JSON.stringify(prefs));
    setSavedAt(new Date().toLocaleTimeString());
  }

  const dirty = useMemo(() => {
    const raw = localStorage.getItem(storageKey);
    const current = raw ? (JSON.parse(raw) as Preferences) : defaultPrefs;
    return JSON.stringify(current) !== JSON.stringify(prefs);
  }, [prefs]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Settings</h1>
          <p className="text-sm text-muted-foreground mt-0.5">Manage workspace-level behavior and AI execution preferences.</p>
        </div>
        <div className="flex items-center gap-2">
          {savedAt && <span className="text-xs text-muted-foreground">Saved at {savedAt}</span>}
          <Button onClick={save} disabled={!dirty}>
            <Save className="h-4 w-4 mr-1.5" /> Save changes
          </Button>
        </div>
      </div>

      <Tabs defaultValue="general" className="space-y-4">
        <TabsList>
          <TabsTrigger value="general">General</TabsTrigger>
          <TabsTrigger value="execution">Execution</TabsTrigger>
          <TabsTrigger value="notifications">Notifications</TabsTrigger>
        </TabsList>

        <TabsContent value="general">
          <Card>
            <CardHeader><CardTitle className="text-base">Organization</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-1.5 max-w-md">
                <Label>Organization name</Label>
                <Input value={prefs.orgName} onChange={(e) => setPrefs((p) => ({ ...p, orgName: e.target.value }))} />
              </div>
              <div className="space-y-1.5 max-w-md">
                <Label>Default environment</Label>
                <Input value={prefs.defaultEnvironment} onChange={(e) => setPrefs((p) => ({ ...p, defaultEnvironment: e.target.value }))} />
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="execution">
          <Card>
            <CardHeader><CardTitle className="text-base">Execution Behavior</CardTitle></CardHeader>
            <CardContent className="space-y-5">
              <div className="flex items-center justify-between max-w-xl">
                <div>
                  <p className="text-sm font-medium">Auto-heal locators</p>
                  <p className="text-xs text-muted-foreground">Allow AI to repair unstable selectors during execution.</p>
                </div>
                <Switch checked={prefs.autoHealLocators} onCheckedChange={(v) => setPrefs((p) => ({ ...p, autoHealLocators: v }))} />
              </div>
              <div className="space-y-1.5 max-w-md">
                <Label>Max parallel executions</Label>
                <Input
                  type="number"
                  min={1}
                  max={30}
                  value={prefs.maxParallelExecutions}
                  onChange={(e) => setPrefs((p) => ({ ...p, maxParallelExecutions: Number(e.target.value || 1) }))}
                />
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="notifications">
          <Card>
            <CardHeader><CardTitle className="text-base">Alert Preferences</CardTitle></CardHeader>
            <CardContent>
              <div className="flex items-center justify-between max-w-xl">
                <div>
                  <p className="text-sm font-medium">Notify on execution failures</p>
                  <p className="text-xs text-muted-foreground">Send immediate alerts for failed runs and flaky test spikes.</p>
                </div>
                <Switch checked={prefs.notifyOnFailure} onCheckedChange={(v) => setPrefs((p) => ({ ...p, notifyOnFailure: v }))} />
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
