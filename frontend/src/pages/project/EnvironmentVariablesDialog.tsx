import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Loader2, KeyRound, Eye, EyeOff } from 'lucide-react';
import { getEnvironmentVariables, updateEnvironmentVariables } from '../../api/environments';
import type { EnvironmentVariable } from '../../api/types';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Alert } from '../../components/ui/alert';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '../../components/ui/dialog';

type FieldType = 'text' | 'secret';
interface Row { key: string; value: string; type: FieldType }

/**
 * Postman-style environment variables editor. Variables are key/value pairs bound as {{key}}
 * during Explore/Run against this environment. Secret fields are masked with a show/hide toggle.
 */
export function EnvironmentVariablesDialog({
  projectId, environmentId, environmentName, onClose,
}: {
  projectId: string;
  environmentId: string;
  environmentName: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [rows, setRows] = useState<Row[]>([]);
  const [revealed, setRevealed] = useState<Record<number, boolean>>({});
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['environment-variables', projectId, environmentId],
    queryFn: () => getEnvironmentVariables(projectId, environmentId),
  });

  useEffect(() => {
    if (!data) return;
    setRows(data.length > 0
      ? data.map((v) => ({ key: v.key, value: v.value ?? '', type: v.type === 'secret' ? 'secret' : 'text' }))
      : [{ key: '', value: '', type: 'text' }]);
  }, [data]);

  const setRow = (idx: number, patch: Partial<Row>) =>
    setRows((prev) => prev.map((r, i) => (i === idx ? { ...r, ...patch } : r)));
  const addRow = () => setRows((prev) => [...prev, { key: '', value: '', type: 'text' }]);
  const removeRow = (idx: number) => setRows((prev) => prev.filter((_, i) => i !== idx));
  const toggleReveal = (idx: number) => setRevealed((prev) => ({ ...prev, [idx]: !prev[idx] }));

  const saveMutation = useMutation({
    mutationFn: () => {
      const cleaned = rows
        .map((r) => ({ key: r.key.trim(), value: r.value, type: r.type }))
        .filter((r) => r.key !== '');
      if (new Set(cleaned.map((r) => r.key)).size !== cleaned.length) {
        throw new Error('Variable keys must be unique.');
      }
      const variables: EnvironmentVariable[] = cleaned.map((r) => ({ key: r.key, value: r.value, type: r.type }));
      return updateEnvironmentVariables(projectId, environmentId, variables);
    },
    onSuccess: () => {
      // Keys sync across all environments server-side, so refresh every environment's variables.
      queryClient.invalidateQueries({ queryKey: ['environment-variables'] });
      onClose();
    },
    onError: (e) => setError(e instanceof Error ? e.message : getErrorMessage(e)),
  });

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-2xl gap-0 p-0 overflow-hidden">
        <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="flex max-h-[85vh] flex-col">
          <DialogHeader className="border-b border-border px-6 py-4">
            <DialogTitle className="flex items-center gap-2">
              <KeyRound className="h-4 w-4 text-violet-500" />
              Environment variables — {environmentName}
            </DialogTitle>
            <p className="text-sm text-muted-foreground">
              Key/value pairs bound as <code className="text-xs">{'{{key}}'}</code> when you Explore or Run against this environment.
            </p>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto px-6 py-4 space-y-3">
            {error && <Alert severity="error" className="text-sm">{error}</Alert>}

            <Alert severity="info" className="text-xs">
              Keys are shared across every environment in this project — adding a key here adds it (empty) to all
              others, so scenarios won&rsquo;t fail when run against a different environment. Only the values differ per environment.
            </Alert>

            {isLoading ? (
              <div className="py-10 text-center text-muted-foreground text-sm">
                <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2" /> Loading…
              </div>
            ) : (
              <div className="rounded-lg border border-border overflow-hidden">
                <div className="grid grid-cols-[1fr_1fr_120px_auto] gap-0 bg-muted/60 text-xs font-medium text-muted-foreground">
                  <div className="px-3 py-2 border-b border-border">Key</div>
                  <div className="px-3 py-2 border-b border-l border-border">Value</div>
                  <div className="px-3 py-2 border-b border-l border-border">Type</div>
                  <div className="px-2 py-2 border-b border-l border-border w-10" />
                </div>
                {rows.map((row, idx) => {
                  const isSecret = row.type === 'secret';
                  const show = revealed[idx];
                  return (
                    <div key={idx} className="grid grid-cols-[1fr_1fr_120px_auto] gap-0 group">
                      <div className="border-b border-border p-0">
                        <input
                          value={row.key}
                          onChange={(e) => setRow(idx, { key: e.target.value })}
                          placeholder="username"
                          className="w-full bg-transparent px-3 py-2 text-sm font-mono outline-none focus:bg-violet-50"
                        />
                      </div>
                      <div className="relative border-b border-l border-border p-0">
                        <input
                          type={isSecret && !show ? 'password' : 'text'}
                          value={row.value}
                          onChange={(e) => setRow(idx, { value: e.target.value })}
                          placeholder={isSecret ? '••••••••' : 'alice@example.com'}
                          className="w-full bg-transparent px-3 py-2 pr-9 text-sm outline-none focus:bg-violet-50"
                        />
                        {isSecret && (
                          <button
                            type="button"
                            onClick={() => toggleReveal(idx)}
                            aria-label={show ? 'Hide value' : 'Show value'}
                            className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                          >
                            {show ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                          </button>
                        )}
                      </div>
                      <div className="border-b border-l border-border flex items-center px-1">
                        <Select value={row.type} onValueChange={(v) => setRow(idx, { type: v as FieldType })}>
                          <SelectTrigger className="h-8 border-0 shadow-none focus:ring-0 text-xs">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="text">Text</SelectItem>
                            <SelectItem value="secret">Secret</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="border-b border-l border-border flex items-center justify-center w-10">
                        <Button type="button" variant="ghost" size="icon" className="h-7 w-7 text-muted-foreground hover:text-destructive"
                          aria-label={`Remove variable ${row.key || idx + 1}`} onClick={() => removeRow(idx)}>
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            <Button type="button" variant="outline" size="sm" onClick={addRow}>
              <Plus className="h-3.5 w-3.5 mr-1" /> Add variable
            </Button>
          </div>

          <DialogFooter className="border-t border-border px-6 py-4">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={saveMutation.isPending}>
              {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
              Save variables
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
