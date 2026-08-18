import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { FileUp, Loader2, UploadCloud } from 'lucide-react';
import { importScenariosFromFile } from '../../api/scenarios';
import { getErrorMessage } from '../../lib/apiClient';
import { Alert } from '../../components/ui/alert';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import { Label } from '../../components/ui/label';

const ACCEPTED = '.csv,.xlsx';

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

  const importMutation = useMutation({
    mutationFn: () => {
      if (!file) {
        throw new Error('Choose a .csv or .xlsx file first.');
      }
      return importScenariosFromFile(projectId, featureId, file);
    },
    onSuccess: (created) => {
      setError(null);
      setFile(null);
      queryClient.invalidateQueries({ queryKey: ['scenarios', projectId] });
      queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
      onImported(created.length);
      onClose();
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Import manual test cases</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}

          <div className="text-sm text-muted-foreground">
            Importing into feature <span className="font-medium text-foreground">{featureLabel}</span>.
            Existing manual scenarios for this feature are replaced.
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
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={importMutation.isPending}>Cancel</Button>
          <Button onClick={() => importMutation.mutate()} disabled={!file || importMutation.isPending}>
            {importMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <FileUp className="h-4 w-4 mr-1.5" />}
            Import scenarios
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
