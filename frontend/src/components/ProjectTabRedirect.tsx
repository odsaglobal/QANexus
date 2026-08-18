import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { listProjects } from '../api/projects';
import { getStoredProjectId, setStoredProjectId } from '../lib/projectSelection';
import { type ProjectTab, tabToPathSegment } from '../lib/projectTabs';

export function ProjectTabRedirect({ tab, title }: { tab: ProjectTab; title: string }) {
  const navigate = useNavigate();
  const projectsQuery = useQuery({
    queryKey: ['projects', { page: 1, pageSize: 100 }],
    queryFn: () => listProjects({ page: 1, pageSize: 100 }),
  });

  useEffect(() => {
    if (projectsQuery.isLoading || projectsQuery.isError) return;

    const projects = projectsQuery.data?.items ?? [];
    if (projects.length === 0) {
      navigate('/projects', { replace: true });
      return;
    }

    const storedProjectId = getStoredProjectId();
    const hasStoredProject = projects.some((project) => project.id === storedProjectId);
    const targetProjectId = hasStoredProject ? storedProjectId : projects[0].id;

    if (!hasStoredProject) {
      setStoredProjectId(targetProjectId);
    }

    const segment = tabToPathSegment(tab);
    navigate(`/projects/${targetProjectId}/${segment}`, { replace: true });
  }, [navigate, projectsQuery.data, projectsQuery.isError, projectsQuery.isLoading, tab]);

  return (
    <div className="space-y-2">
      <h1 className="text-2xl font-bold text-foreground">{title}</h1>
      <p className="text-sm text-muted-foreground">Loading your selected project...</p>
    </div>
  );
}
