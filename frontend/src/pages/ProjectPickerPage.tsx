import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Loader2, FolderOpen } from 'lucide-react';
import { listProjects } from '../api/projects';
import { getStoredProjectId, setStoredProjectId } from '../lib/projectSelection';
import type { ProjectTab } from '../lib/projectTabs';
import { tabToPathSegment } from '../lib/projectTabs';
import { Card, CardContent } from '../components/ui/card';
import { Button } from '../components/ui/button';

interface Props {
  title: string;
  tab: ProjectTab;
}

export function ProjectPickerPage({ title, tab }: Props) {
  const navigate = useNavigate();
  const storedId = getStoredProjectId();

  const { data, isLoading } = useQuery({
    queryKey: ['projects-picker'],
    queryFn: () => listProjects({ pageSize: 100 }),
  });

  const projects = data?.items ?? [];

  // Auto-redirect if a stored project still exists in the list
  useEffect(() => {
    if (!storedId || !projects.length) return;
    const match = projects.find((p) => p.id === storedId);
    if (match) {
      navigate(`/projects/${storedId}/${tabToPathSegment(tab)}`, { replace: true });
    }
  }, [storedId, projects, navigate, tab]);

  function pick(id: string) {
    setStoredProjectId(id);
    navigate(`/projects/${id}/${tabToPathSegment(tab)}`);
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading projects…
      </div>
    );
  }

  return (
    <div className="max-w-xl mx-auto mt-16 space-y-6 px-4">
      <div>
        <h1 className="text-2xl font-bold text-foreground">{title}</h1>
        <p className="text-muted-foreground text-sm mt-1">Select a project to continue.</p>
      </div>

      {projects.length === 0 ? (
        <Card>
          <CardContent className="py-10 text-center text-muted-foreground text-sm">
            No projects yet. Create one from the Projects page.
            <div className="mt-4">
              <Button variant="outline" onClick={() => navigate('/projects')}>Go to Projects</Button>
            </div>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-2">
          {projects.map((project) => (
            <button
              key={project.id}
              type="button"
              onClick={() => pick(project.id)}
              className="w-full text-left rounded-lg border border-border px-4 py-3 hover:bg-muted transition-colors flex items-center gap-3"
            >
              <FolderOpen className="h-4 w-4 text-violet-500 flex-shrink-0" />
              <div>
                <div className="text-sm font-medium text-foreground">{project.name}</div>
                {project.description && (
                  <div className="text-xs text-muted-foreground mt-0.5 line-clamp-1">{project.description}</div>
                )}
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
