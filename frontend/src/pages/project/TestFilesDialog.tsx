import { useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileUp, Loader2, Paperclip, Trash2 } from 'lucide-react';
import { deleteTestFile, listTestFiles, uploadTestFile } from '../../api/testFiles';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Alert } from '../../components/ui/alert';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';

const formatSize = (bytes: number) =>
  bytes < 1024 ? `${bytes} B` : bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

/** Files that scenarios may attach in upload steps. Tests can upload only these, never other server files. */
export function TestFilesDialog({ projectId, onClose }: { projectId: string; onClose: () => void }) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);

  const { data: files = [], isLoading } = useQuery({
    queryKey: ['test-files', projectId],
    queryFn: () => listTestFiles(projectId),
  });

  const uploadMutation = useMutation({
    mutationFn: async (selected: File[]) => {
      for (const file of selected) {
        await uploadTestFile(projectId, file);
      }
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['test-files', projectId] }),
  });

  const deleteMutation = useMutation({
    mutationFn: (name: string) => deleteTestFile(projectId, name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['test-files', projectId] }),
  });

  const error = uploadMutation.error ?? deleteMutation.error;

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Paperclip className="h-4 w-4 text-violet-500" /> Test files
          </DialogTitle>
          <p className="text-sm text-muted-foreground">
            Files your scenarios can attach in upload steps, e.g. <em>Upload resume.pdf</em>. Max 25 MB each.
          </p>
        </DialogHeader>

        <div className="min-w-0 space-y-3">
          {error && <Alert severity="error" className="text-sm">{getErrorMessage(error)}</Alert>}

          <div className="rounded-lg border border-border divide-y divide-border max-h-[50vh] overflow-y-auto">
            {isLoading ? (
              <div className="py-8 text-center text-sm text-muted-foreground">
                <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2" /> Loading…
              </div>
            ) : files.length === 0 ? (
              <div className="py-8 text-center text-sm text-muted-foreground">No test files yet.</div>
            ) : files.map((f) => (
              <div key={f.name} className="flex items-center gap-3 px-3 py-2 text-sm">
                <span className="min-w-0 flex-1 truncate font-mono" title={f.name}>{f.name}</span>
                <span className="text-xs text-muted-foreground">{formatSize(f.sizeBytes)}</span>
                <Button type="button" variant="ghost" size="icon" className="h-7 w-7 text-muted-foreground hover:text-destructive"
                  aria-label={`Delete ${f.name}`} disabled={deleteMutation.isPending}
                  onClick={() => deleteMutation.mutate(f.name)}>
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </div>
            ))}
          </div>

          <input
            ref={inputRef}
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              const selected = Array.from(e.target.files ?? []);
              e.target.value = '';
              if (selected.length > 0) uploadMutation.mutate(selected);
            }}
          />
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>Close</Button>
          <Button type="button" onClick={() => inputRef.current?.click()} disabled={uploadMutation.isPending}>
            {uploadMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <FileUp className="h-4 w-4 mr-1.5" />}
            Upload files
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
