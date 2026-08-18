import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Upload, Sparkles, Trash2, ChevronDown, Loader2 } from 'lucide-react';
import type { Requirement } from '../../api/types';
import {
  analyzeRequirement, deleteRequirement, getRequirement,
  listRequirements, uploadRequirement,
} from '../../api/requirements';
import { getErrorMessage } from '../../lib/apiClient';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Alert } from '../../components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '../../components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../components/ui/table';
import { cn } from '../../lib/utils';
import { useConfirm } from '../../components/ui/confirm-dialog';

export function RequirementsTab({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const { confirm } = useConfirm();
  const [error, setError] = useState<string | null>(null);
  const [analyzingId, setAnalyzingId] = useState<string | null>(null);

  const { data: requirements = [] } = useQuery({
    queryKey: ['requirements', projectId],
    queryFn: () => listRequirements(projectId),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadRequirement(projectId, file.name, file),
    onSuccess: () => { setError(null); queryClient.invalidateQueries({ queryKey: ['requirements', projectId] }); },
    onError: (e) => setError(getErrorMessage(e)),
  });

  const analyzeMutation = useMutation({
    mutationFn: (id: string) => analyzeRequirement(projectId, id),
    onMutate: (id) => setAnalyzingId(id),
    onSettled: () => setAnalyzingId(null),
    onSuccess: () => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ['requirements', projectId] });
      queryClient.invalidateQueries({ queryKey: ['knowledge-graph', projectId] });
    },
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
              <CardTitle>Requirement Intelligence</CardTitle>
              <CardDescription>Upload SRS/BRD/Swagger docs; AI extracts modules, features and user stories.</CardDescription>
            </div>
            <Button variant="outline" size="sm" asChild>
              <label className="cursor-pointer">
                {uploadMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <Upload className="h-4 w-4 mr-1.5" />}
                Upload document
                <input type="file" hidden accept=".pdf,.docx,.md,.markdown,.txt,.json,.yaml,.yml" onChange={handleFile} />
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
                <TableHead>Status</TableHead>
                <TableHead className="text-center">Modules</TableHead>
                <TableHead className="text-center">Features</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {requirements.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-10 text-muted-foreground text-sm">
                    No documents uploaded yet.
                  </TableCell>
                </TableRow>
              )}
              {requirements.map((req) => (
                <RequirementRow key={req.id} projectId={projectId} requirement={req}
                  analyzing={analyzingId === req.id}
                  onAnalyze={() => analyzeMutation.mutate(req.id)}
                  onDelete={async () => {
                    if (await confirm({ title: 'Delete document?', description: `"${req.name}" and its extracted analysis will be removed.`, confirmText: 'Delete', tone: 'destructive' })) {
                      deleteMutation.mutate(req.id);
                    }
                  }} />
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

function RequirementRow({ projectId, requirement, analyzing, onAnalyze, onDelete }: {
  projectId: string; requirement: Requirement; analyzing: boolean; onAnalyze: () => void; onDelete: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const detailQuery = useQuery({
    queryKey: ['requirement', projectId, requirement.id, requirement.status],
    queryFn: () => getRequirement(projectId, requirement.id),
    enabled: expanded && requirement.status === 'Analyzed',
  });

  return (
    <>
      <TableRow>
        <TableCell>
          <div className="font-medium text-sm">{requirement.name}</div>
          {requirement.errorMessage && <div className="text-xs text-red-500 mt-0.5">{requirement.errorMessage}</div>}
        </TableCell>
        <TableCell><Badge variant="outline">{requirement.sourceType}</Badge></TableCell>
        <TableCell>
          <Badge variant={requirement.status === 'Analyzed' ? 'success' : requirement.status === 'Failed' ? 'warning' : 'info'}>
            {requirement.status}
          </Badge>
        </TableCell>
        <TableCell className="text-center text-sm">{requirement.moduleCount}</TableCell>
        <TableCell className="text-center text-sm">{requirement.featureCount}</TableCell>
        <TableCell>
          <div className="flex items-center gap-1 justify-end">
            {requirement.status === 'Analyzed' && (
              <Button variant="ghost" size="icon" className="h-8 w-8" aria-label={expanded ? 'Collapse details' : 'Expand details'} aria-expanded={expanded} onClick={() => setExpanded((v) => !v)}>
                <ChevronDown className={cn('h-4 w-4 transition-transform', expanded && 'rotate-180')} />
              </Button>
            )}
            <Button variant="ghost" size="sm" className="h-8 text-xs"
              disabled={analyzing || requirement.status === 'Failed'} onClick={onAnalyze}>
              {analyzing ? <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" /> : <Sparkles className="h-3.5 w-3.5 mr-1" />}
              {requirement.status === 'Analyzed' ? 'Re-analyze' : 'Analyze'}
            </Button>
            <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-destructive" aria-label={`Delete ${requirement.name}`} onClick={onDelete}>
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        </TableCell>
      </TableRow>
      {expanded && (
        <TableRow>
          <TableCell colSpan={6} className="bg-gray-50/50 px-6 py-3">
            {detailQuery.isLoading && <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />}
            <div className="space-y-3">
              {detailQuery.data?.modules.map((module) => (
                <div key={module.id} className="border border-border rounded-lg">
                  <div className="flex items-center gap-2 px-4 py-2.5 bg-white rounded-lg">
                    <span className="text-sm font-semibold">{module.name}</span>
                    <Badge variant="secondary">{module.features.length} features</Badge>
                  </div>
                  <div className="px-4 pb-3 pt-1 space-y-2">
                    {module.features.map((feature) => (
                      <div key={feature.id} className="pl-3 border-l-2 border-violet-300">
                        <div className="flex items-center gap-2 mb-0.5">
                          <span className="text-sm font-semibold">{feature.name}</span>
                          <Badge variant="outline" className="text-xs">{feature.priority}</Badge>
                        </div>
                        {feature.description && <p className="text-xs text-muted-foreground mb-1">{feature.description}</p>}
                        {feature.userStories.map((story) => (
                          <p key={story.id} className="text-xs text-muted-foreground">
                            • As a {story.asA}, I want {story.iWant}{story.soThat ? `, so that ${story.soThat}` : ''}
                          </p>
                        ))}
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
