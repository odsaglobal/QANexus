import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, FolderPlus } from 'lucide-react';
import { createManualFeature } from '../../api/requirements';
import { getErrorMessage } from '../../lib/apiClient';
import { Alert } from '../../components/ui/alert';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Textarea } from '../../components/ui/textarea';

interface ManualFeatureDialogProps {
  projectId: string;
  open: boolean;
  onClose: () => void;
  onCreated: (featureId: string, featureLabel: string) => void;
}

export function ManualFeatureDialog({ projectId, open, onClose, onCreated }: ManualFeatureDialogProps) {
  const queryClient = useQueryClient();
  const [featureName, setFeatureName] = useState('');
  const [moduleName, setModuleName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: () => createManualFeature(projectId, {
      featureName: featureName.trim(),
      moduleName: moduleName.trim() || undefined,
      description: description.trim() || undefined,
    }),
    onSuccess: (feature) => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ['requirements', projectId] });
      queryClient.invalidateQueries({ queryKey: ['requirement-features', projectId] });
      onCreated(feature.featureId, `${feature.moduleName} › ${feature.featureName}`);
      onClose();
    },
    onError: (e) => setError(getErrorMessage(e)),
  });

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>New manual feature</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-1">
          {error && <Alert severity="error" className="text-sm">{error}</Alert>}
          <p className="text-sm text-muted-foreground">
            Create a feature to author scenarios by hand — no document upload required.
          </p>

          <div className="space-y-1.5">
            <Label>Feature name</Label>
            <Input value={featureName} onChange={(e) => { setError(null); setFeatureName(e.target.value); }} placeholder="e.g. User login" autoFocus />
          </div>
          <div className="space-y-1.5">
            <Label>Module <span className="text-muted-foreground">(optional)</span></Label>
            <Input value={moduleName} onChange={(e) => setModuleName(e.target.value)} placeholder="e.g. Authentication" />
          </div>
          <div className="space-y-1.5">
            <Label>Description / context <span className="text-muted-foreground">(optional)</span></Label>
            <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3}
              placeholder="Short context used to ground AI generation for this feature." />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={createMutation.isPending}>Cancel</Button>
          <Button onClick={() => createMutation.mutate()} disabled={!featureName.trim() || createMutation.isPending}>
            {createMutation.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <FolderPlus className="h-4 w-4 mr-1.5" />}
            Create feature
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
