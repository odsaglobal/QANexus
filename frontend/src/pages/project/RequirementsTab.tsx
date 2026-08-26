import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Upload, Trash2, Loader2, FileText, Eye, Pencil, Save, X } from 'lucide-react';
import type { Requirement } from '../../api/types';
import { deleteRequirement, getRequirement, listRequirements, updateRequirementContent, uploadRequirement } from '../../api/requirements';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Textarea } from '../../components/ui/textarea';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../components/ui/dialog';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { useConfirm } from '../../components/ui/confirm-dialog';

// Hidden internal buckets that back scenarios — never shown as business-context documents.
const HIDDEN_DOCS = new Set(['General', 'AI Explorations']);

export function RequirementsTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [error, setError] = useState<string | null>(null);
  const [previewDoc, setPreviewDoc] = useState<Requirement | null>(null);

  const { data: requirements = [] } = useQuery({
    queryKey: ['requirements', projectId],
    queryFn: () => listRequirements(projectId),
  });

  const docs = requirements.filter((r) => !HIDDEN_DOCS.has(r.name));

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadRequirement(projectId, file.name, file),
    onSuccess: () => { setError(null); queryClient.invalidateQueries({ queryKey: ['requirements', projectId] }); },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteRequirement(projectId, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['requirements', projectId] }),
  });

  const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) uploadMutation.mutate(file);
    e.target.value = '';
  };

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle>Business Context</CardTitle>
              <CardDescription>
                Upload domain documents (Markdown, text, PDF, DOCX). The AI reads them while exploring your app and
                when generating test cases — so it understands your terminology and rules.
              </CardDescription>
            </div>
            <Button variant="outline" size="sm" asChild>
              <label className="cursor-pointer">
                {uploadMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Upload className="h-4 w-4 mr-1.5" />}
                Upload document
                <input type="file" hidden accept=".md,.markdown,.txt,.pdf,.docx,.json,.yaml,.yml" onChange={handleFile} />
              </label>
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {error && <div className="px-6 pb-3"><Alert severity="error" className="text-sm">{error}</Alert></div>}
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Document</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Uploaded</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {docs.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center py-10 text-muted-foreground text-sm">
                    No business-context documents yet. Upload a .md or .txt file describing your domain.
                  </TableCell>
                </TableRow>
              )}
              {docs.map((req: Requirement) => (
                <TableRow key={req.id}>
                  <TableCell>
                    <div className="font-medium text-sm flex items-center gap-2">
                      <FileText className="h-4 w-4 text-violet-500 shrink-0" />
                      {req.name}
                    </div>
                    {req.errorMessage && <div className="text-xs text-red-500 mt-0.5">{req.errorMessage}</div>}
                  </TableCell>
                  <TableCell><Badge variant="outline">{req.sourceType}</Badge></TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {new Date(req.createdAtUtc).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1 justify-end">
                      <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-foreground" aria-label={`View ${req.name}`}
                        onClick={() => { setError(null); setPreviewDoc(req); }}>
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" aria-label={`Delete ${req.name}`}
                        onClick={async () => {
                          if (await confirm({ title: 'Delete document?', description: `"${req.name}" will be removed from the business context.`, confirmText: 'Delete', tone: 'destructive' })) {
                            deleteMutation.mutate(req.id);
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
        </CardContent>
      </Card>

      {previewDoc && (
        <DocumentPreviewDialog
          projectId={projectId}
          doc={previewDoc}
          onClose={() => setPreviewDoc(null)}
        />
      )}
    </div>
  );
}

function DocumentPreviewDialog({
  projectId, doc, onClose,
}: {
  projectId: string;
  doc: Requirement;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  const { data: detail, isLoading } = useQuery({
    queryKey: ['requirement', projectId, doc.id],
    queryFn: () => getRequirement(projectId, doc.id),
  });

  const content = detail?.content ?? '';

  const saveMutation = useMutation({
    mutationFn: () => updateRequirementContent(projectId, doc.id, draft),
    onSuccess: (updated) => {
      setError(null);
      setEditing(false);
      queryClient.setQueryData(['requirement', projectId, doc.id], updated);
      queryClient.invalidateQueries({ queryKey: ['requirements', projectId] });
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const startEdit = () => { setDraft(content); setEditing(true); };

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-3xl gap-0 p-0 overflow-hidden">
        <DialogHeader className="border-b border-border px-6 py-4">
          <DialogTitle className="flex items-center gap-2">
            <FileText className="h-4 w-4 text-violet-500" />
            {doc.name}
            <Badge variant="outline" className="ml-1">{doc.sourceType}</Badge>
          </DialogTitle>
        </DialogHeader>

        <div className="px-6 py-4 max-h-[65vh] overflow-y-auto">
          {error && <Alert severity="error" className="text-sm mb-3">{error}</Alert>}
          {isLoading ? (
            <div className="py-10 text-center text-muted-foreground text-sm">
              <Loader2 className="h-5 w-5 animate-spin mx-auto mb-2" /> Loading content…
            </div>
          ) : editing ? (
            <Textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              rows={20}
              className="font-mono text-xs leading-relaxed"
              autoFocus
            />
          ) : content ? (
            <pre className="whitespace-pre-wrap break-words font-mono text-xs leading-relaxed text-foreground">
              {content}
            </pre>
          ) : (
            <p className="py-10 text-center text-muted-foreground text-sm">
              This document has no extracted text. Click Edit to add content.
            </p>
          )}
        </div>

        <DialogFooter className="border-t border-border px-6 py-4">
          {editing ? (
            <>
              <Button variant="outline" onClick={() => setEditing(false)} disabled={saveMutation.isPending}>
                <X className="h-4 w-4 mr-1.5" /> Cancel
              </Button>
              <Button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}>
                {saveMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Save className="h-4 w-4 mr-1.5" />}
                Save changes
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={onClose}>Close</Button>
              <Button onClick={startEdit} disabled={isLoading}>
                <Pencil className="h-4 w-4 mr-1.5" /> Edit
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
