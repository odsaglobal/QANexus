import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2 } from 'lucide-react';
import { createManualScenario, type ManualScenarioStepInput } from '../../api/scenarios';
import { getErrorMessage } from '../../lib/apiClient';
import { Alert } from '../../components/ui/alert';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Textarea } from '../../components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';

const SCENARIO_TYPES = ['Positive', 'Negative', 'Boundary', 'Regression', 'Smoke', 'Sanity', 'Accessibility', 'Security', 'Api'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];
const RISKS = ['Low', 'Medium', 'High'];

interface EditableStep { key: string; action: string; expectedResult: string; }

interface ManualScenarioDialogProps {
  projectId: string;
  featureId: string;
  featureLabel: string;
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}

export function ManualScenarioDialog({ projectId, featureId, featureLabel, open, onClose, onCreated }: ManualScenarioDialogProps) {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState('');
  const [type, setType] = useState('Positive');
  const [priority, setPriority] = useState('Medium');
  const [risk, setRisk] = useState('Medium');
  const [preconditions, setPreconditions] = useState('');
  const [expectedResult, setExpectedResult] = useState('');
  const [steps, setSteps] = useState<EditableStep[]>([{ key: crypto.randomUUID(), action: '', expectedResult: '' }]);
  const [error, setError] = useState<string | null>(null);

  const updateStep = (key: string, field: 'action' | 'expectedResult', value: string) =>
    setSteps((prev) => prev.map((s) => (s.key === key ? { ...s, [field]: value } : s)));
  const addStep = () => setSteps((prev) => [...prev, { key: crypto.randomUUID(), action: '', expectedResult: '' }]);
  const removeStep = (key: string) => setSteps((prev) => (prev.length === 1 ? prev : prev.filter((s) => s.key !== key)));

  const createMutation = useMutation({
    mutationFn: () => {
      const payloadSteps = steps
        .filter((s) => s.action.trim())
        .map<ManualScenarioStepInput>((s) => ({ action: s.action.trim(), expectedResult: s.expectedResult.trim() || undefined }));
      if (payloadSteps.length === 0) {
        throw new Error('Add at least one step with an action.');
      }
      return createManualScenario({
        projectId, featureId, title: title.trim(), type, priority, risk,
        preconditions: preconditions.trim() || undefined,
        expectedResult: expectedResult.trim() || undefined,
        steps: payloadSteps,
      });
    },
    onSuccess: () => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
      onCreated();
      onClose();
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add manual scenario</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}
          <p className="text-sm text-muted-foreground">
            Adding to feature <span className="font-medium text-foreground">{featureLabel}</span>.
            Include a URL in a step to drive scenario-based exploration.
          </p>

          <div className="space-y-1.5">
            <Label>Title</Label>
            <Input value={title} onChange={(e) => { setError(null); setTitle(e.target.value); }} placeholder="e.g. Login with valid credentials" autoFocus />
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select value={type} onValueChange={setType}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{SCENARIO_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Priority</Label>
              <Select value={priority} onValueChange={setPriority}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{PRIORITIES.map((p) => <SelectItem key={p} value={p}>{p}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Risk</Label>
              <Select value={risk} onValueChange={setRisk}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{RISKS.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}</SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Preconditions <span className="text-muted-foreground">(optional)</span></Label>
            <Textarea value={preconditions} onChange={(e) => setPreconditions(e.target.value)} rows={2}
              placeholder="What must be true before this scenario runs?" />
          </div>

          <div>
            <div className="flex items-center justify-between mb-2">
              <Label>Steps</Label>
              <span className="text-xs text-muted-foreground">{steps.filter((s) => s.action.trim()).length} steps</span>
            </div>
            <div className="space-y-2">
              {steps.map((step, index) => (
                <div key={step.key} className="grid grid-cols-[28px_1fr_auto] gap-2 items-start">
                  <div className="h-6 w-6 rounded-full bg-violet-100 text-violet-700 text-xs font-bold flex items-center justify-center mt-1">{index + 1}</div>
                  <div className="space-y-1">
                    <Textarea value={step.action} onChange={(e) => updateStep(step.key, 'action', e.target.value)} rows={2}
                      placeholder="Action, e.g. Navigate to /login and enter valid credentials" className="text-sm resize-none" />
                    <Input value={step.expectedResult} onChange={(e) => updateStep(step.key, 'expectedResult', e.target.value)}
                      placeholder="Expected result (optional)" className="text-xs" />
                  </div>
                  <Button type="button" variant="ghost" size="icon" className="h-7 w-7 text-muted-foreground hover:text-destructive"
                    disabled={steps.length === 1} onClick={() => removeStep(step.key)}>
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              ))}
            </div>
            <Button type="button" variant="outline" size="sm" className="mt-2" onClick={addStep}>
              <Plus className="h-4 w-4 mr-1.5" /> Add step
            </Button>
          </div>

          <div className="space-y-1.5">
            <Label>Overall expected result <span className="text-muted-foreground">(optional)</span></Label>
            <Textarea value={expectedResult} onChange={(e) => setExpectedResult(e.target.value)} rows={2}
              placeholder="Summarise the outcome when all steps succeed." />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={createMutation.isPending}>Cancel</Button>
          <Button onClick={() => createMutation.mutate()} disabled={!title.trim() || createMutation.isPending}>
            {createMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
            Create scenario
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
