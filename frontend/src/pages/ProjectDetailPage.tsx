import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getProject } from '../api/projects';
import { OverviewTab } from './project/OverviewTab';
import { EnvironmentsTab } from './project/EnvironmentsTab';
import { RequirementsTab } from './project/RequirementsTab';
import { ScenariosTab } from './project/ScenariosTab';
import { SuitesTab } from './project/SuitesTab';
import { KnowledgeGraphTab } from './project/KnowledgeGraphTab';
import { ExplorerTab } from './project/ExplorerTab';
import { Tabs, TabsContent } from '../components/ui/tabs';
import { isProjectTab, pathSegmentToTab, tabToPathSegment, type ProjectTab } from '../lib/projectTabs';
import { getStoredProjectId, clearStoredProjectId } from '../lib/projectSelection';

export function ProjectDetailPage() {
  const { projectId = '', tab: tabSegment } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const tabFromPath = pathSegmentToTab(tabSegment);
  const legacyTabParam = searchParams.get('tab');
  const tabFromLegacyQuery = isProjectTab(legacyTabParam) ? legacyTabParam : null;
  const resolvedTab: ProjectTab = tabFromPath ?? tabFromLegacyQuery ?? 'overview';

  const [tab, setTab] = useState<ProjectTab>(resolvedTab);

  useEffect(() => {
    const nextTab: ProjectTab = resolvedTab;
    setTab((current) => (current === nextTab ? current : nextTab));
  }, [resolvedTab]);

  useEffect(() => {
    if (!projectId) return;
    if (tabFromPath) return;
    if (!tabFromLegacyQuery || tabFromLegacyQuery === 'overview') return;
    navigate(`/projects/${projectId}/${tabToPathSegment(tabFromLegacyQuery)}`, { replace: true });
  }, [navigate, projectId, tabFromLegacyQuery, tabFromPath]);

  const projectQuery = useQuery({
    queryKey: ['project', projectId],
    queryFn: () => getProject(projectId),
    enabled: Boolean(projectId),
    retry: false,
  });

  // A stale selected project (e.g. saved from a different database/tenant) will 404.
  // Clear the stored selection and return to the projects list instead of wedging the UI.
  useEffect(() => {
    if (!projectQuery.isError) return;
    if (getStoredProjectId() === projectId) {
      clearStoredProjectId();
    }
    navigate('/projects', { replace: true });
  }, [projectQuery.isError, projectId, navigate]);

  return (
    <div className="space-y-5 lg:space-y-6">
      {/* Tabs (navigation provided by the sidebar; content is driven by the route) */}
      <Tabs value={tab}>
        <TabsContent value="overview" className="mt-6">
          <OverviewTab projectId={projectId} />
        </TabsContent>
        <TabsContent value="requirements" className="mt-6">
          <RequirementsTab projectId={projectId} />
        </TabsContent>
        <TabsContent value="scenarios" className="mt-6">
          <ScenariosTab projectId={projectId} />
        </TabsContent>
        <TabsContent value="suites" className="mt-6">
          <SuitesTab projectId={projectId} />
        </TabsContent>
        <TabsContent value="graph" className="mt-6">
          <KnowledgeGraphTab projectId={projectId} />
        </TabsContent>
        <TabsContent value="explorer" className="mt-6">
          <ExplorerTab projectId={projectId} />
        </TabsContent>
        <TabsContent value="environments" className="mt-6">
          <EnvironmentsTab projectId={projectId} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
