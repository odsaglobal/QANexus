import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileUp, Loader2, UploadCloud } from 'lucide-react';
import {
  downloadImportTemplate,
  importScenariosFromFile,
  type ImportSuiteMode,
} from '../../api/scenarios';
import { listTestSuites } from '../../api/suites';
import { getErrorMessage } from '../../lib/apiClient';
import { Alert } from '../../components/ui/alert';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';

const ACCEPTED = '.csv,.xlsx';

const SUITE_CHOICES: { value: ImportSuiteMode; label: string; hint: string }[] = [
  { value: 'None', label: 'No suite', hint: 'Import the test cases on their own.' },
  { value: 'Existing', label: 'Add to an existing suite', hint: 'Append to the end of a suite you already have.' },
  { value: 'New', label: 'Create a new suite', hint: 'Make a suite containing exactly these test cases.' },
];

interface ImportScenariosDialogProps {
  projectId: string;
  featureId: string;
  featureLabel: string;
  open: boolean;
  onClose: () => void;
  onImported: (count: number) => void;
}

export function ImportScenariosDialog({
  projectId,
  featureId,
  featureLabel,
  open,
  onClose,
  onImported,
}: ImportScenariosDialogProps) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [suiteMode, setSuiteMode] = useState<ImportSuiteMode>('None');
  const [suiteId, setSuiteId] = useState('');
  const [newSuiteName, setNewSuiteName] = useState('');

  const suitesQuery = useQuery({
    queryKey: ['test-suites', projectId],
    queryFn: () => listTestSuites(projectId),
    enabled: open,
  });

  const templateMutation = useMutation({
    mutationFn: () => downloadImportTemplate(projectId),
    onError: (e) => setError(getErrorMessage(e)),
  });

  const importMutation = useMutation({
    mutationFn: () => {
      if (!file) {
        throw new Error('Choose a .csv or .xlsx file first.');
      }
      return importScenariosFromFile(projectId, featureId, file, {
        mode: suiteMode,
        suiteId: suiteId || undefined,
        newSuiteName: newSuiteName.trim() || undefined,
      });
    },
    onSuccess: (result) => {
      setError(null);
      setFile(null);
      setSuiteMode('None');
      setSuiteId('');
      setNewSuiteName('');
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
      queryClient.invalidateQueries({ queryKey: ['test-suites', projectId] });
      onImported(result.scenarios.length);
      onClose();
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const suites = suitesQuery.data ?? [];
  const suiteChoiceIncomplete =
    (suiteMode === 'Existing' && !suiteId) || (suiteMode === 'New' && !newSuiteName.trim());

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Import manual test cases</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          <div className="flex items-start justify-between gap-3">
            <div className="text-sm text-muted-foreground">
              Importing into feature <span className="font-medium text-foreground">{featureLabel}</span>.
              Imported test cases are added alongside the existing ones.
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="shrink-0"
              onClick={() => templateMutation.mutate()}
              disabled={templateMutation.isPending}
            >
              {templateMutation.isPending
                ? <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                : <Download className="h-3.5 w-3.5 mr-1.5" />}
              Template
            </Button>
          </div>

          <div className="space-y-1.5">
            <Label>Test case file (.csv or .xlsx)</Label>
            <button
              type="button"
              onClick={() => inputRef.current?.click()}
              className="w-full rounded-lg border border-dashed border-input px-4 py-8 text-center hover:border-violet-300 hover:bg-violet-50/40 transition-colors"
            >
              <UploadCloud className="h-7 w-7 text-violet-500 mx-auto mb-2" />
              <p className="text-sm font-medium text-foreground">
                {file ? file.name : 'Click to choose a file'}
              </p>
              <p className="text-xs text-muted-foreground mt-1">
                One row per step. Columns: title, step, step_expected, optional case_id, preconditions, expected_result, step_order.
              </p>
            </button>
            <input
              ref={inputRef}
              type="file"
              accept={ACCEPTED}
              className="hidden"
              onChange={(e) => {
                setError(null);
                setFile(e.target.files?.[0] ?? null);
              }}
            />
          </div>

          {file && (
            <div className="space-y-2 border-t border-border pt-4">
              <div>
                <Label>Add these test cases to a suite?</Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  {file.name} is ready to import. Choose where the test cases should go.
                </p>
              </div>
              <div className="space-y-1.5">
                {SUITE_CHOICES.map((choice) => (
                  <label
                    key={choice.value}
                    className={`flex cursor-pointer items-start gap-2.5 rounded-lg border px-3 py-2 transition-colors ${
                      suiteMode === choice.value
                        ? 'border-violet-300 bg-violet-50/60'
                        : 'border-input hover:bg-muted/40'
                    }`}
                  >
                    <input
                      type="radio"
                      name="suite-mode"
                      className="mt-1 accent-violet-600"
                      value={choice.value}
                      checked={suiteMode === choice.value}
                      onChange={() => {
                        setError(null);
                        setSuiteMode(choice.value);
                      }}
                    />
                    <span className="min-w-0">
                      <span className="block text-sm font-medium text-foreground">{choice.label}</span>
                      <span className="block text-xs text-muted-foreground">{choice.hint}</span>
                    </span>
                  </label>
                ))}
              </div>

              {suiteMode === 'Existing' && (
                <div className="pt-1">
                  {suitesQuery.isLoading ? (
                    <p className="text-xs text-muted-foreground">Loading suites…</p>
                  ) : suites.length === 0 ? (
                    <p className="text-xs text-muted-foreground">
                      This project has no suites yet — choose “Create a new suite” instead.
                    </p>
                  ) : (
                    <select
                      value={suiteId}
                      onChange={(e) => setSuiteId(e.target.value)}
                      className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
                    >
                      <option value="">Select a suite…</option>
                      {suites.map((s) => (
                        <option key={s.id} value={s.id}>{s.name}</option>
                      ))}
                    </select>
                  )}
                </div>
              )}

            {suiteMode === 'New' && (
                <div className="pt-1">
                  <Input
                    value={newSuiteName}
                    onChange={(e) => setNewSuiteName(e.target.value)}
                    placeholder="e.g. Checkout regression"
                    maxLength={200}
                  />
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={importMutation.isPending}>Cancel</Button>
          <Button
            onClick={() => importMutation.mutate()}
            disabled={!file || suiteChoiceIncomplete || importMutation.isPending}
          >
            {importMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <FileUp className="h-4 w-4 mr-1.5" />}
            Import scenarios
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
