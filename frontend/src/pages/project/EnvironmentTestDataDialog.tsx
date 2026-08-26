import { useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Plus, Search, Pencil, Trash2, Loader2, Database, Table as TableIcon, X, FileJson, FileUp,
} from 'lucide-react';
import * as XLSX from 'xlsx';
import {
  listTestDataSets, createTestDataSet, updateTestDataSet, deleteTestDataSet,
} from '../../api/testData';
import type { TestDataRow, TestDataSet } from '../../api/types';
import { getErrorMessage } from '../../lib/apiClient';
import { Badge } from '../../components/ui/badge';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Alert } from '../../components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '../../components/ui/dialog';
import { useConfirm } from '../../components/ui/confirm-dialog';

/**
 * Manages the environment-specific test data sets for a single environment.
 * Opened from the Environments tab so test data lives alongside the environment it belongs to.
 */
export function EnvironmentTestDataDialog({
  projectId, environmentId, environmentName, onClose,
}: {
  projectId: string;
  environmentId: string;
  environmentName: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [query, setQuery] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<TestDataSet | null>(null);

  const dataSetsQuery = useQuery({
    queryKey: ['test-data', projectId, environmentId],
    queryFn: () => listTestDataSets(projectId, environmentId),
  });
  const dataSets = dataSetsQuery.data ?? [];

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return dataSets;
    return dataSets.filter((d) =>
      d.name.toLowerCase().includes(q)
      || (d.description ?? '').toLowerCase().includes(q)
      || d.columns.some((c) => c.toLowerCase().includes(q)));
  }, [dataSets, query]);

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteTestDataSet(projectId, environmentId, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['test-data', projectId, environmentId] }),
    onError: (e) => setError(getErrorMessage(e)),
  });

  // Postman-style variable tokens exposed by these datasets (first row = active values).
  const variableTokens = useMemo(() => {
    const tokens: string[] = [];
    const seen = new Set<string>();
    for (const ds of dataSets) {
      for (const col of ds.columns) {
        if (!seen.has(col.toLowerCase())) { seen.add(col.toLowerCase()); tokens.push(col); }
      }
    }
    return tokens;
  }, [dataSets]);

  return (
    <>
      <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
        <DialogContent className="max-w-3xl gap-0 p-0 overflow-hidden">
          <DialogHeader className="border-b border-border px-6 py-4">
            <DialogTitle className="flex items-center gap-2">
              <Database className="h-4 w-4 text-violet-500" />
              Test data — {environmentName}
            </DialogTitle>
            <p className="text-sm text-muted-foreground">
              Datasets (accounts, cards, records) specific to this environment.
            </p>
          </DialogHeader>

          <div className="px-6 py-4 space-y-3">
            {error && <Alert severity="error" className="text-sm">{error}</Alert>}

            <div className="flex items-center gap-2">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search datasets..." className="pl-9" />
              </div>
              <Button onClick={() => { setError(null); setEditing(null); setEditorOpen(true); }}>
                <Plus className="h-4 w-4 mr-1.5" /> New dataset
              </Button>
            </div>

            {variableTokens.length > 0 && (
              <div className="rounded-lg border border-violet-200 bg-violet-50/60 px-3 py-2">
                <p className="text-xs text-violet-800 font-medium mb-1">
                  Dataset columns also bind as variables (first row = active value) when you Explore or Run:
                </p>
                <div className="flex flex-wrap gap-1">
                  {variableTokens.map((t) => (
                    <code key={t} className="rounded bg-white border border-violet-200 px-1.5 py-0.5 text-[11px] text-violet-700">{`{{${t}}}`}</code>
                  ))}
                </div>
              </div>
            )}

            <div className="rounded-lg border border-border overflow-hidden">
              <div className="max-h-[52vh] overflow-y-auto">
                {dataSetsQuery.isLoading ? (
                  <div className="py-10 text-center text-muted-foreground text-sm">
                    <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2" /> Loading datasets…
                  </div>
                ) : filtered.length === 0 ? (
                  <div className="py-10 text-center text-muted-foreground text-sm">
                    {dataSets.length === 0
                      ? 'No datasets for this environment yet. Create one to store accounts, cards or records.'
                      : 'No datasets match your search.'}
                  </div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Name</TableHead>
                        <TableHead>Columns</TableHead>
                        <TableHead>Rows</TableHead>
                        <TableHead className="w-20 text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filtered.map((ds) => (
                        <TableRow key={ds.id}>
                          <TableCell>
                            <div className="font-medium">{ds.name}</div>
                            {ds.description && <div className="text-xs text-muted-foreground truncate max-w-xs">{ds.description}</div>}
                          </TableCell>
                          <TableCell>
                            <div className="flex flex-wrap gap-1 max-w-[220px]">
                              {ds.columns.slice(0, 3).map((c) => <Badge key={c} variant="outline" className="text-[10px]">{c}</Badge>)}
                              {ds.columns.length > 3 && <Badge variant="secondary" className="text-[10px]">+{ds.columns.length - 3}</Badge>}
                            </div>
                          </TableCell>
                          <TableCell>{ds.rowCount}</TableCell>
                          <TableCell>
                            <div className="flex items-center justify-end gap-1">
                              <Button variant="ghost" size="icon" className="h-8 w-8" aria-label={`Edit ${ds.name}`}
                                onClick={() => { setError(null); setEditing(ds); setEditorOpen(true); }}>
                                <Pencil className="h-4 w-4" />
                              </Button>
                              <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" aria-label={`Delete ${ds.name}`}
                                onClick={async () => {
                                  if (await confirm({ title: 'Delete dataset?', description: `"${ds.name}" will be permanently removed from this environment.`, confirmText: 'Delete', tone: 'destructive' })) {
                                    deleteMutation.mutate(ds.id);
                                  }
                                }}>
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </div>
            </div>
          </div>

          <DialogFooter className="border-t border-border px-6 py-3">
            <Button type="button" variant="outline" onClick={onClose}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {editorOpen && (
        <TestDataEditorDialog
          projectId={projectId}
          environmentId={environmentId}
          environmentName={environmentName}
          dataSet={editing}
          onClose={() => setEditorOpen(false)}
          onSaved={() => {
            setEditorOpen(false);
            queryClient.invalidateQueries({ queryKey: ['test-data', projectId, environmentId] });
          }}
        />
      )}
    </>
  );
}

function TestDataEditorDialog({
  projectId, environmentId, environmentName, dataSet, onClose, onSaved,
}: {
  projectId: string;
  environmentId: string;
  environmentName: string;
  dataSet: TestDataSet | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(dataSet?.name ?? '');
  const [description, setDescription] = useState(dataSet?.description ?? '');
  const [columns, setColumns] = useState<string[]>(dataSet?.columns.length ? [...dataSet.columns] : ['username', 'password']);
  // Rows stored index-aligned to columns for stable rename/insert/delete.
  const [rows, setRows] = useState<string[][]>(() => {
    const cols = dataSet?.columns.length ? dataSet.columns : ['username', 'password'];
    if (!dataSet || dataSet.rows.length === 0) return [cols.map(() => '')];
    return dataSet.rows.map((r) => cols.map((c) => r[c] ?? ''));
  });
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const jsonInputRef = useRef<HTMLInputElement>(null);
  const excelInputRef = useRef<HTMLInputElement>(null);

  const renameColumn = (idx: number, value: string) =>
    setColumns((prev) => prev.map((c, i) => (i === idx ? value : c)));

  const addColumn = () => {
    setColumns((prev) => [...prev, `column${prev.length + 1}`]);
    setRows((prev) => prev.map((r) => [...r, '']));
  };

  const removeColumn = (idx: number) => {
    setColumns((prev) => prev.filter((_, i) => i !== idx));
    setRows((prev) => prev.map((r) => r.filter((_, i) => i !== idx)));
  };

  const setCell = (rowIdx: number, colIdx: number, value: string) =>
    setRows((prev) => prev.map((r, i) => (i === rowIdx ? r.map((c, j) => (j === colIdx ? value : c)) : r)));

  const addRow = () => setRows((prev) => [...prev, columns.map(() => '')]);
  const removeRow = (idx: number) => setRows((prev) => prev.filter((_, i) => i !== idx));

  // Replaces the grid with imported columns/rows.
  const applyImport = (cols: string[], importedRows: string[][], sourceLabel: string) => {
    if (cols.length === 0) {
      setError('No columns found in the imported file.');
      return;
    }
    setColumns(cols);
    setRows(importedRows.length > 0 ? importedRows : [cols.map(() => '')]);
    setError(null);
    setNotice(`Imported ${importedRows.length} row(s) and ${cols.length} column(s) from ${sourceLabel}.`);
  };

  const importJson = async (file: File) => {
    try {
      const parsed = JSON.parse(await file.text());
      let cols: string[] = [];
      let dataRows: Record<string, unknown>[] = [];
      if (Array.isArray(parsed)) {
        dataRows = parsed as Record<string, unknown>[];
        const seen = new Set<string>();
        for (const obj of dataRows) {
          for (const k of Object.keys(obj ?? {})) if (!seen.has(k)) { seen.add(k); cols.push(k); }
        }
      } else if (parsed && Array.isArray(parsed.columns) && Array.isArray(parsed.rows)) {
        cols = parsed.columns.map(String);
        dataRows = parsed.rows as Record<string, unknown>[];
      } else {
        throw new Error('JSON must be an array of objects, or an object with "columns" and "rows".');
      }
      const rowArrays = dataRows.map((obj) => cols.map((c) => {
        const v = (obj ?? {})[c];
        return v === null || v === undefined ? '' : String(v);
      }));
      applyImport(cols, rowArrays, file.name);
    } catch (e) {
      setNotice(null);
      setError(e instanceof Error ? `Could not import JSON: ${e.message}` : 'Could not import JSON.');
    }
  };

  const importSheet = async (file: File) => {
    try {
      const buf = await file.arrayBuffer();
      const wb = XLSX.read(buf, { type: 'array' });
      const sheet = wb.Sheets[wb.SheetNames[0]];
      if (!sheet) throw new Error('The file has no sheets.');
      const aoa = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, blankrows: false, defval: '' });
      if (aoa.length === 0) throw new Error('The sheet is empty.');
      const cols = (aoa[0] as unknown[]).map((c) => String(c ?? '').trim()).filter((c) => c !== '');
      const rowArrays = aoa.slice(1).map((r) => cols.map((_, i) => {
        const v = (r as unknown[])[i];
        return v === null || v === undefined ? '' : String(v);
      }));
      applyImport(cols, rowArrays, file.name);
    } catch (e) {
      setNotice(null);
      setError(e instanceof Error ? `Could not import file: ${e.message}` : 'Could not import file.');
    }
  };

  const saveMutation = useMutation({
    mutationFn: () => {
      const trimmedColumns = columns.map((c) => c.trim());
      if (trimmedColumns.some((c) => !c)) {
        throw new Error('Column names cannot be empty.');
      }
      if (new Set(trimmedColumns.map((c) => c.toLowerCase())).size !== trimmedColumns.length) {
        throw new Error('Column names must be unique.');
      }
      const rowObjects: TestDataRow[] = rows
        .filter((r) => r.some((v) => v.trim() !== ''))
        .map((r) => {
          const obj: TestDataRow = {};
          trimmedColumns.forEach((c, i) => { obj[c] = r[i] ?? ''; });
          return obj;
        });
      const payload = {
        projectId,
        environmentId,
        name: name.trim(),
        description: description.trim() || undefined,
        columns: trimmedColumns,
        rows: rowObjects,
      };
      return dataSet ? updateTestDataSet(dataSet.id, payload) : createTestDataSet(payload);
    },
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : getErrorMessage(e)),
  });

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-4xl gap-0 p-0 overflow-hidden">
        <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="flex max-h-[88vh] flex-col">
          <DialogHeader className="border-b border-border px-6 py-4">
            <DialogTitle className="flex items-center gap-2">
              <TableIcon className="h-4 w-4 text-violet-500" />
              {dataSet ? 'Edit dataset' : 'New dataset'}
            </DialogTitle>
            <p className="text-sm text-muted-foreground">
              Stored against environment <span className="font-medium text-foreground">{environmentName}</span>.
            </p>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
            {error && <Alert severity="error" className="text-sm">{error}</Alert>}
            {notice && <Alert severity="success" className="text-sm">{notice}</Alert>}

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Name</Label>
                <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Checkout users" required autoFocus />
              </div>
              <div className="space-y-1.5">
                <Label>Description <span className="text-muted-foreground font-normal">(optional)</span></Label>
                <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What this data is used for…" />
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2 rounded-lg border border-dashed border-border bg-muted/30 px-3 py-2.5">
              <span className="text-xs font-medium text-muted-foreground mr-1">Import:</span>
              <Button type="button" variant="outline" size="sm" onClick={() => jsonInputRef.current?.click()}>
                <FileJson className="h-3.5 w-3.5 mr-1.5" /> JSON
              </Button>
              <Button type="button" variant="outline" size="sm" onClick={() => excelInputRef.current?.click()}>
                <FileUp className="h-3.5 w-3.5 mr-1.5" /> Excel / CSV
              </Button>
              <span className="text-xs text-muted-foreground">replaces the grid below — review before saving.</span>
              <input
                ref={jsonInputRef} type="file" accept=".json,application/json" className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) importJson(f); e.target.value = ''; }}
              />
              <input
                ref={excelInputRef} type="file" accept=".xlsx,.xls,.csv" className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) importSheet(f); e.target.value = ''; }}
              />
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Columns &amp; rows</Label>
                <div className="flex items-center gap-2">
                  <Button type="button" size="sm" variant="outline" onClick={addColumn}>
                    <Plus className="h-3.5 w-3.5 mr-1" /> Column
                  </Button>
                  <Button type="button" size="sm" variant="outline" onClick={addRow} disabled={columns.length === 0}>
                    <Plus className="h-3.5 w-3.5 mr-1" /> Row
                  </Button>
                </div>
              </div>

              <div className="overflow-auto rounded-lg border border-border max-h-[42vh]">
                <table className="w-full border-collapse text-sm">
                  <thead className="sticky top-0 bg-muted/60 backdrop-blur">
                    <tr>
                      <th className="w-8 border-b border-border px-2 py-1.5 text-left text-xs font-medium text-muted-foreground">#</th>
                      {columns.map((col, colIdx) => (
                        <th key={colIdx} className="border-b border-l border-border px-1.5 py-1.5 min-w-[140px]">
                          <div className="flex items-center gap-1">
                            <Input
                              value={col}
                              onChange={(e) => renameColumn(colIdx, e.target.value)}
                              className="h-7 text-xs font-medium"
                              placeholder={`column${colIdx + 1}`}
                            />
                            <Button type="button" variant="ghost" size="icon" className="h-6 w-6 text-muted-foreground hover:text-destructive shrink-0"
                              aria-label={`Remove column ${col}`}
                              onClick={() => removeColumn(colIdx)} disabled={columns.length <= 1}>
                              <X className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {rows.length === 0 ? (
                      <tr>
                        <td colSpan={columns.length + 1} className="px-3 py-6 text-center text-xs text-muted-foreground">
                          No rows. Click “Row” to add one.
                        </td>
                      </tr>
                    ) : rows.map((row, rowIdx) => (
                      <tr key={rowIdx} className="group">
                        <td className="border-b border-border px-1 py-1 text-center align-middle">
                          <div className="flex items-center justify-center gap-1">
                            <span className="text-xs text-muted-foreground group-hover:hidden">{rowIdx + 1}</span>
                            <Button type="button" variant="ghost" size="icon" className="h-6 w-6 hidden group-hover:inline-flex text-muted-foreground hover:text-destructive"
                              aria-label={`Remove row ${rowIdx + 1}`} onClick={() => removeRow(rowIdx)}>
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        </td>
                        {columns.map((_, colIdx) => (
                          <td key={colIdx} className="border-b border-l border-border p-0">
                            <input
                              value={row[colIdx] ?? ''}
                              onChange={(e) => setCell(rowIdx, colIdx, e.target.value)}
                              className="w-full bg-transparent px-2 py-1.5 text-sm outline-none focus:bg-violet-50"
                            />
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="text-xs text-muted-foreground">Empty rows are dropped on save. Column names must be unique.</p>
            </div>
          </div>

          <DialogFooter className="border-t border-border px-6 py-4">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={!name.trim() || columns.length === 0 || saveMutation.isPending}>
              {saveMutation.isPending && <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />}
              {dataSet ? 'Save changes' : 'Create dataset'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
