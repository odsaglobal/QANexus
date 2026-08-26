export const projectTabs = [
  'overview',
  'requirements',
  'scenarios',
  'explorer',
  'environments',
] as const;

export type ProjectTab = (typeof projectTabs)[number];

const tabToPathSegmentMap: Record<ProjectTab, string> = {
  overview: 'overview',
  requirements: 'requirements',
  scenarios: 'scenarios',
  explorer: 'explorer',
  environments: 'environments',
};

const pathSegmentToTabMap: Record<string, ProjectTab> = {
  overview: 'overview',
  requirements: 'requirements',
  scenarios: 'scenarios',
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
  if (pathname === '/explorer') return 'explorer';
  return null;
}
