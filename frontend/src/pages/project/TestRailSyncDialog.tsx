import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, RefreshCw } from 'lucide-react';
import { syncTestRailScenarios } from '../../api/scenarios';
import { getErrorMessage } from '../../lib/apiClient';
import { Alert } from '../../components/ui/alert';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';

interface TestRailSyncDialogProps {
  projectId: string;
  featureId: string;
  featureLabel: string;
  open: boolean;
  onClose: () => void;
  onSynced: (count: number) => void;
}

export function TestRailSyncDialog({
  projectId,
  featureId,
  featureLabel,
  open,
  onClose,
  onSynced,
}: TestRailSyncDialogProps) {
  const queryClient = useQueryClient();
  const [testRailProjectId, setTestRailProjectId] = useState('');
  const [suiteId, setSuiteId] = useState('');
  const [sectionId, setSectionId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const syncMutation = useMutation({
    mutationFn: () => {
      const parsedProjectId = Number(testRailProjectId);
      if (!Number.isInteger(parsedProjectId) || parsedProjectId <= 0) {
        throw new Error('Enter a valid numeric TestRail project id.');
      }
      return syncTestRailScenarios({
        projectId,
        featureId,
        testRailProjectId: parsedProjectId,
        suiteId: suiteId ? Number(suiteId) : undefined,
        sectionId: sectionId ? Number(sectionId) : undefined,
      });
    },
    onSuccess: (created) => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
      onSynced(created.length);
      onClose();
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Sync from TestRail</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          <div className="text-sm text-muted-foreground">
            Syncing into feature <span className="font-medium text-foreground">{featureLabel}</span>.
            Existing TestRail scenarios for this feature are replaced.
          </div>

          <div className="space-y-1.5">
            <Label>TestRail project id</Label>
            <Input
              inputMode="numeric"
              value={testRailProjectId}
              onChange={(e) => { setError(null); setTestRailProjectId(e.target.value.replace(/[^0-9]/g, '')); }}
              placeholder="e.g. 12"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Suite id <span className="text-muted-foreground">(optional)</span></Label>
              <Input
                inputMode="numeric"
                value={suiteId}
                onChange={(e) => setSuiteId(e.target.value.replace(/[^0-9]/g, ''))}
                placeholder="e.g. 3"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Section id <span className="text-muted-foreground">(optional)</span></Label>
              <Input
                inputMode="numeric"
                value={sectionId}
                onChange={(e) => setSectionId(e.target.value.replace(/[^0-9]/g, ''))}
                placeholder="e.g. 45"
              />
            </div>
          </div>

          <p className="text-xs text-muted-foreground">
            TestRail connection (base URL, user and API key) is configured on the server.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={syncMutation.isPending}>Cancel</Button>
          <Button onClick={() => syncMutation.mutate()} disabled={!testRailProjectId || syncMutation.isPending}>
            {syncMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <RefreshCw className="h-4 w-4 mr-1.5" />}
            Sync scenarios
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
