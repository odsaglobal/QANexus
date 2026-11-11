import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { getTenantSettings, updateTenantSettings } from '../api/tenant';
import { getErrorMessage } from '../lib/apiClient';
import { cn } from '../lib/utils';
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

const retryOptions = [0, 1, 2, 3];

/** Workspace-wide (server-side) setting: re-run failed tests automatically during Run. */
function RetrySettingCard() {
  const queryClient = useQueryClient();
  const settingsQuery = useQuery({ queryKey: ['tenant-settings'], queryFn: getTenantSettings });
  const saveMutation = useMutation({
    mutationFn: (failedTestRetries: number) => updateTenantSettings({ failedTestRetries }),
    onSuccess: (data) => queryClient.setQueryData(['tenant-settings'], data),
  });

  const current = saveMutation.isPending ? saveMutation.variables : settingsQuery.data?.failedTestRetries;

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Retry failed tests</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p className="text-xs text-muted-foreground max-w-xl">
          When a test fails during a run, run it again in a fresh browser session up to this many times. Only the
          last attempt is reported, and a test that passes on a retry is flagged as possibly flaky in the run log.
        </p>
        <div className="flex items-center gap-3">
          <div className="inline-flex rounded-md border border-border p-0.5" role="radiogroup" aria-label="Retries for failed tests">
            {retryOptions.map((n) => (
              <button
                key={n}
                type="button"
                role="radio"
                aria-checked={current === n}
                disabled={settingsQuery.isLoading || saveMutation.isPending}
                onClick={() => saveMutation.mutate(n)}
                className={cn(
                  'rounded px-3 py-1 text-sm transition-colors disabled:opacity-60',
                  current === n ? 'bg-primary text-primary-foreground' : 'text-foreground hover:bg-muted',
                )}
              >
                {n === 0 ? 'Off' : `${n}×`}
              </button>
            ))}
          </div>
          {saveMutation.isPending && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          {saveMutation.isSuccess && !saveMutation.isPending && <span className="text-xs text-muted-foreground">Saved</span>}
        </div>
        {(settingsQuery.isError || saveMutation.isError) && (
          <p className="text-xs text-red-600">{getErrorMessage(saveMutation.error ?? settingsQuery.error)}</p>
        )}
      </CardContent>
    </Card>
  );
}

const defaultPrefs: Preferences = {
  orgName: 'Patterns.ai Organization',
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

        <TabsContent value="execution" className="space-y-4">
          <RetrySettingCard />
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
