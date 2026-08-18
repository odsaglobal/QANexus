export const projectTabs = [
  'overview',
  'requirements',
  'scenarios',
  'suites',
  'graph',
  'explorer',
  'environments',
] as const;

export type ProjectTab = (typeof projectTabs)[number];

const tabToPathSegmentMap: Record<ProjectTab, string> = {
  overview: 'overview',
  requirements: 'requirements',
  scenarios: 'scenarios',
  suites: 'suites',
  graph: 'knowledge-graph',
  explorer: 'explorer',
  environments: 'environments',
};

const pathSegmentToTabMap: Record<string, ProjectTab> = {
  overview: 'overview',
  requirements: 'requirements',
  scenarios: 'scenarios',
  suites: 'suites',
  graph: 'graph',
  'knowledge-graph': 'graph',
  explorer: 'explorer',
  environments: 'environments',
};

export function isProjectTab(value: string | null | undefined): value is ProjectTab {
  return Boolean(value && projectTabs.includes(value as ProjectTab));
}

export function tabToPathSegment(tab: ProjectTab): string {
  return tabToPathSegmentMap[tab];
}

export function pathSegmentToTab(segment: string | null | undefined): ProjectTab | null {
  if (!segment) return null;
  return pathSegmentToTabMap[segment] ?? null;
}

export function entryRouteToTab(pathname: string): ProjectTab | null {
  if (pathname === '/requirements') return 'requirements';
  if (pathname === '/scenarios') return 'scenarios';
  if (pathname === '/knowledge-graph') return 'graph';
  if (pathname === '/explorer') return 'explorer';
  return null;
}
