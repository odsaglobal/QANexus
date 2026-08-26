import { useEffect, useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, ArrowUp, ArrowDown, Loader2, AlertTriangle, Link2, ExternalLink } from 'lucide-react';
import type { JiraIssue, Scenario } from '../../api/types';
import { deleteScenario, updateScenario, applyProposedSteps, discardProposedSteps, revertSteps, getJiraIssue, type UpdateScenarioStepInput } from '../../api/scenarios';
import { getErrorMessage } from '../../lib/apiClient';
import { cn } from '../../lib/utils';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Label } from '../../components/ui/label';
import { Input } from '../../components/ui/input';
import { Textarea } from '../../components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import { Separator } from '../../components/ui/separator';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog';
import { useConfirm } from '../../components/ui/confirm-dialog';

const SCENARIO_TYPES = ['Positive','Negative','Boundary','Regression','Smoke','Sanity','Accessibility','Security','Api','CrossBrowser','Performance'];
const PRIORITIES = ['Low','Medium','High','Critical'];
const RISKS = ['Low','Medium','High'];

interface EditableStep { key: string; action: string; expectedResult: string; needsReview?: boolean; reviewReason?: string | null; }

export function ScenarioEditorDialog({ scenario, projectId, open, onClose }: {
  scenario: Scenario; projectId: string; open: boolean; onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [focusKey, setFocusKey] = useState<string | null>(null);
  const [title, setTitle] = useState(scenario.title);
  const [type, setType] = useState(scenario.type);
  const [priority, setPriority] = useState(scenario.priority);
  const [risk, setRisk] = useState(scenario.risk);
  const [preconditions, setPreconditions] = useState(scenario.preconditions ?? '');
  const [expectedResult, setExpectedResult] = useState(scenario.expectedResult ?? '');
  const [jiraKey, setJiraKey] = useState(scenario.jiraKey ?? '');
  const [jiraIssue, setJiraIssue] = useState<JiraIssue | null>(null);
  const [steps, setSteps] = useState<EditableStep[]>(() =>
    scenario.steps.map((s) => ({ key: crypto.randomUUID(), action: s.action, expectedResult: s.expectedResult ?? '', needsReview: s.needsReview, reviewReason: s.reviewReason })),
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setTitle(scenario.title); setType(scenario.type); setPriority(scenario.priority);
    setRisk(scenario.risk); setPreconditions(scenario.preconditions ?? '');
    setExpectedResult(scenario.expectedResult ?? '');
    setJiraKey(scenario.jiraKey ?? '');
    setJiraIssue(null);
    setSteps(scenario.steps.map((s) => ({ key: crypto.randomUUID(), action: s.action, expectedResult: s.expectedResult ?? '', needsReview: s.needsReview, reviewReason: s.reviewReason })));
    setError(null);
  }, [scenario]);

  const updateStep = (key: string, field: 'action' | 'expectedResult', value: string) =>
    setSteps((prev) => prev.map((s) => (s.key === key ? { ...s, [field]: value } : s)));
  const insertStep = (afterIndex: number) => {
    const newKey = crypto.randomUUID();
    setSteps((prev) => { const next = [...prev]; next.splice(afterIndex + 1, 0, { key: newKey, action: '', expectedResult: '' }); return next; });
    setFocusKey(newKey);
  };
  const appendStep = () => {
    const newKey = crypto.randomUUID();
    setSteps((prev) => [...prev, { key: newKey, action: '', expectedResult: '' }]);
    setFocusKey(newKey);
  };
  const removeStep = (key: string) => setSteps((prev) => prev.filter((s) => s.key !== key));
  const moveStep = (index: number, dir: 'up' | 'down') => {
    setSteps((prev) => {
      const next = [...prev]; const target = dir === 'up' ? index - 1 : index + 1;
      [next[index], next[target]] = [next[target], next[index]]; return next;
    });
  };

  const saveMutation = useMutation({
    mutationFn: () => updateScenario(projectId, {
      id: scenario.id, title: title.trim(), type, priority, risk,
      preconditions: preconditions.trim() || undefined,
      expectedResult: expectedResult.trim() || undefined,
      jiraKey: jiraKey.trim() || undefined,
      tags: scenario.tags,
      steps: steps.filter((s) => s.action.trim()).map<UpdateScenarioStepInput>((s) => ({
        action: s.action.trim(), expectedResult: s.expectedResult.trim() || undefined,
      })),
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] }); onClose(); },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const jiraFetch = useMutation({
    mutationFn: () => getJiraIssue(projectId, jiraKey),
    onSuccess: (issue) => {
      setError(null);
      setJiraIssue(issue);
      setJiraKey(issue.key);
      if (!title.trim()) setTitle(issue.summary);
      if (!preconditions.trim() && issue.description) setPreconditions(issue.description);
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteScenario(projectId, scenario.id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] }); onClose(); },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const applyMutation = useMutation({
    mutationFn: () => applyProposedSteps(projectId, scenario.id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] }); onClose(); },
    onError: (e) => setError(getErrorMessage(e)),
  });
  const discardMutation = useMutation({
    mutationFn: () => discardProposedSteps(projectId, scenario.id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] }); onClose(); },
    onError: (e) => setError(getErrorMessage(e)),
  });
  const revertMutation = useMutation({
    mutationFn: () => revertSteps(projectId, scenario.id),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] }); onClose(); },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const proposedSteps = scenario.proposedSteps ?? [];

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center justify-between">
            <DialogTitle>Edit scenario</DialogTitle>
            <Badge variant="outline">
              {scenario.source === 'AiGenerated' ? 'AI-generated' : scenario.source === 'TestRail' ? 'TestRail' : 'Manual'}
            </Badge>
          </div>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          {proposedSteps.length > 0 && (
            <div className="rounded-lg border border-violet-200 bg-violet-50 p-3 space-y-2">
              <div className="flex items-center gap-2 text-sm font-semibold text-violet-800">
                <AlertTriangle className="h-4 w-4" />
                Suggested steps from exploring the real app ({proposedSteps.length})
              </div>
              <p className="text-xs text-violet-700">
                These are the actual actions the AI performed on the app. Applying them replaces the current steps (your originals are backed up and can be reverted).
              </p>
              <ol className="text-xs text-foreground space-y-1 list-decimal pl-5">
                {proposedSteps.map((s, i) => (
                  <li key={i}>{s.action}</li>
                ))}
              </ol>
              <div className="flex items-center gap-2 pt-1">
                <Button type="button" size="sm" disabled={applyMutation.isPending} onClick={() => applyMutation.mutate()}>
                  {applyMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
                  Apply suggested steps
                </Button>
                <Button type="button" variant="outline" size="sm" disabled={discardMutation.isPending} onClick={() => discardMutation.mutate()}>
                  Discard
                </Button>
              </div>
            </div>
          )}

          {scenario.canRevert && (
            <div className="flex items-center justify-between rounded-lg border border-border bg-gray-50 p-2.5">
              <span className="text-xs text-muted-foreground">A previous step set is backed up.</span>
              <Button type="button" variant="outline" size="sm" disabled={revertMutation.isPending} onClick={() => revertMutation.mutate()}>
                {revertMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
                Revert to previous steps
              </Button>
            </div>
          )}

          <div className="space-y-1.5">
            <Label>Title</Label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} required autoFocus />
          </div>

          <div className="space-y-1.5">
            <Label className="flex items-center gap-1.5"><Link2 className="h-3.5 w-3.5" /> Jira ticket <span className="text-muted-foreground font-normal">(optional)</span></Label>
            <div className="flex items-center gap-2">
              <Input value={jiraKey} onChange={(e) => { setJiraKey(e.target.value); setJiraIssue(null); }}
                placeholder="PROJ-123" className="font-mono" />
              <Button type="button" variant="outline" disabled={!jiraKey.trim() || jiraFetch.isPending}
                onClick={() => jiraFetch.mutate()}>
                {jiraFetch.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : null}
                Fetch
              </Button>
            </div>
            {jiraIssue && (
              <div className="rounded-lg border border-border bg-muted/40 p-2.5 text-xs space-y-1">
                <div className="flex items-center gap-2">
                  <a href={jiraIssue.url} target="_blank" rel="noreferrer" className="font-mono font-semibold text-violet-600 hover:underline flex items-center gap-1">
                    {jiraIssue.key} <ExternalLink className="h-3 w-3" />
                  </a>
                  {jiraIssue.issueType && <Badge variant="outline">{jiraIssue.issueType}</Badge>}
                  {jiraIssue.status && <Badge variant="secondary">{jiraIssue.status}</Badge>}
                  {jiraIssue.priority && <Badge variant="outline">{jiraIssue.priority}</Badge>}
                </div>
                <div className="font-medium text-foreground">{jiraIssue.summary}</div>
                {jiraIssue.description && <p className="text-muted-foreground line-clamp-4 whitespace-pre-wrap">{jiraIssue.description}</p>}
              </div>
            )}
            <p className="text-[11px] text-muted-foreground">Enter a Jira key and Fetch to pull details, or just type a reference manually.</p>
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
            <Label>Preconditions</Label>
            <Textarea value={preconditions} onChange={(e) => setPreconditions(e.target.value)}
              placeholder="What must be true before this scenario can run?" rows={2} />
          </div>

          <Separator />

          <div>
            <div className="flex items-center justify-between mb-3">
              <div>
                <p className="text-sm font-semibold">Steps</p>
                <p className="text-xs text-muted-foreground">Shift+Enter to insert a step below</p>
              </div>
              <span className="text-xs text-muted-foreground">{steps.filter((s) => s.action.trim()).length} steps</span>
            </div>

            <div className="space-y-1">
              {steps.map((step, index) => (
                <StepRow key={step.key} index={index} total={steps.length}
                  action={step.action} expectedResult={step.expectedResult}
                  needsReview={step.needsReview} reviewReason={step.reviewReason}
                  shouldFocus={focusKey === step.key}
                  onFocused={() => setFocusKey(null)}
                  onActionChange={(v) => updateStep(step.key, 'action', v)}
                  onExpectedChange={(v) => updateStep(step.key, 'expectedResult', v)}
                  onMoveUp={() => moveStep(index, 'up')}
                  onMoveDown={() => moveStep(index, 'down')}
                  onRemove={() => removeStep(step.key)}
                  onInsertBelow={() => insertStep(index)} />
              ))}
            </div>

            <Button type="button" variant="outline" size="sm" className="mt-2" onClick={appendStep}>
              <Plus className="h-4 w-4 mr-1.5" /> Add step
            </Button>
          </div>

          <Separator />

          <div className="space-y-1.5">
            <Label>Overall expected result</Label>
            <Textarea value={expectedResult} onChange={(e) => setExpectedResult(e.target.value)}
              placeholder="Summarise the overall outcome when all steps succeed." rows={2} />
          </div>
        </div>

        <DialogFooter className="justify-between">
          <Button type="button" variant="destructive" size="sm"
            disabled={deleteMutation.isPending || saveMutation.isPending}
            onClick={async () => {
              if (await confirm({ title: 'Delete scenario?', description: `"${scenario.title}" will be permanently removed.`, confirmText: 'Delete', tone: 'destructive' })) {
                deleteMutation.mutate();
              }
            }}>
            {deleteMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Trash2 className="h-4 w-4 mr-1.5" />}
            Delete
          </Button>
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={saveMutation.isPending}>Cancel</Button>
            <Button type="button" disabled={!title.trim() || saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
              Save changes
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function StepRow({ index, total, action, expectedResult, needsReview, reviewReason, shouldFocus, onFocused,
  onActionChange, onExpectedChange, onMoveUp, onMoveDown, onRemove, onInsertBelow }: {
  index: number; total: number; action: string; expectedResult: string;
  needsReview?: boolean; reviewReason?: string | null;
  shouldFocus: boolean; onFocused: () => void;
  onActionChange: (v: string) => void; onExpectedChange: (v: string) => void;
  onMoveUp: () => void; onMoveDown: () => void; onRemove: () => void; onInsertBelow: () => void;
}) {
  const actionRef = useRef<HTMLTextAreaElement>(null);
  useEffect(() => { if (shouldFocus) { actionRef.current?.focus(); onFocused(); } }, [shouldFocus, onFocused]);

  return (
    <div className={cn(
      'group grid grid-cols-[32px_1fr_auto] gap-2 items-start p-2 rounded-lg transition-colors',
      needsReview ? 'bg-amber-50 ring-1 ring-amber-300' : 'hover:bg-gray-50',
    )}>
      <div className={cn(
        'h-6 w-6 rounded-full text-xs font-bold flex items-center justify-center mt-1 flex-shrink-0',
        needsReview ? 'bg-amber-200 text-amber-800' : 'bg-violet-100 text-violet-700',
      )}>
        {index + 1}
      </div>
      <div className="space-y-1">
        <Textarea ref={actionRef} value={action} onChange={(e) => onActionChange(e.target.value)}
          placeholder="Describe the action…" rows={2} className="text-sm resize-none"
          onKeyDown={(e) => { if (e.key === 'Enter' && e.shiftKey) { e.preventDefault(); onInsertBelow(); } }} />
        <Input value={expectedResult} onChange={(e) => onExpectedChange(e.target.value)}
          placeholder="Expected result (optional)" className="text-xs" />
        {needsReview && reviewReason && (
          <p className="flex items-start gap-1.5 text-xs text-amber-700">
            <AlertTriangle className="h-3.5 w-3.5 mt-0.5 shrink-0" />
            <span>Needs review: {reviewReason} Refine this step, then re-run to auto-heal.</span>
          </p>
        )}
      </div>
      <div className="flex flex-col gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity">
        <Button type="button" variant="ghost" size="icon" className="h-6 w-6" disabled={index === 0} onClick={onMoveUp}>
          <ArrowUp className="h-3 w-3" />
        </Button>
        <Button type="button" variant="ghost" size="icon" className="h-6 w-6" disabled={index === total - 1} onClick={onMoveDown}>
          <ArrowDown className="h-3 w-3" />
        </Button>
        <Button type="button" variant="ghost" size="icon" className="h-6 w-6 text-muted-foreground hover:text-destructive" onClick={onRemove}>
          <Trash2 className="h-3 w-3" />
        </Button>
      </div>
    </div>
  );
}
