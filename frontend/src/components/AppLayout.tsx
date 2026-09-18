import { useEffect, useState } from 'react';
import { Link as RouterLink, Outlet, matchPath, useLocation, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  LayoutDashboard, FolderOpen, FileText, FlaskConical,
  Play, BarChart2, Settings, ChevronDown,
  HelpCircle, Search, LogOut, User, Activity, Shield,
  Lightbulb, Compass, Server, ExternalLink,
} from 'lucide-react';
import { listEnvironments } from '../api/environments';
import { listProjects } from '../api/projects';
import { useAuthStore } from '../store/authStore';
import { useAuth0 } from '@auth0/auth0-react';
import { AtipLogo, AtipWordmark } from './AtipLogo';
import { NotificationBell } from './NotificationBell';
import { CommandSearch } from './CommandSearch';
import { OnboardingGate } from './OnboardingGate';
import { UserAvatar } from './UserAvatar';
import { Button } from './ui/button';
import { Separator } from './ui/separator';
import { cn } from '../lib/utils';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger,
} from './ui/dropdown-menu';

/** Public documentation site (separate VitePress project). Configurable per deployment. */
const DOCS_URL = import.meta.env.VITE_DOCS_URL ?? 'https://valyt2026.github.io/QANexus-docs/';

const simpleNav: { label: string; to: string; icon: React.ReactNode; section?: string; external?: boolean }[] = [
  { label: 'Dashboard',      to: '/',               icon: <LayoutDashboard className="h-4 w-4" /> },
  { label: 'Projects',       to: '/projects',       icon: <FolderOpen className="h-4 w-4" />,      section: 'Projects' },
  { label: 'Environments',   to: '/environments',   icon: <Server className="h-4 w-4" />,           section: 'Projects' },
  { label: 'Business Context', to: '/requirements',   icon: <FileText className="h-4 w-4" />,         section: 'AI Engine' },
  { label: 'Scenarios',      to: '/scenarios',      icon: <FlaskConical className="h-4 w-4" />,     section: 'AI Engine' },
  { label: 'Explorer',       to: '/explorer',       icon: <Compass className="h-4 w-4" />,          section: 'AI Engine' },
  { label: 'Executions',     to: '/executions',     icon: <Play className="h-4 w-4" />,             section: 'Execution' },
  { label: 'Reports',        to: '/reports',        icon: <BarChart2 className="h-4 w-4" />,        section: 'Execution' },
  { label: 'AI Insights',    to: '/ai-insights',    icon: <Lightbulb className="h-4 w-4" />,       section: 'Execution' },
  { label: 'Agent Monitor',  to: '/agents',         icon: <Activity className="h-4 w-4" />,         section: 'System' },
  { label: 'Settings',       to: '/settings',       icon: <Settings className="h-4 w-4" />,         section: 'System' },
  { label: 'Administration', to: '/administration', icon: <Shield className="h-4 w-4" />,           section: 'System' },
  { label: 'Help',           to: DOCS_URL,          icon: <HelpCircle className="h-4 w-4" />,       section: 'System', external: true },
];

const navSections = [
  { heading: undefined, items: simpleNav.filter((i) => !i.section) },
  { heading: 'Projects',   items: simpleNav.filter((i) => i.section === 'Projects') },
  { heading: 'AI Engine',  items: simpleNav.filter((i) => i.section === 'AI Engine') },
  { heading: 'Execution',  items: simpleNav.filter((i) => i.section === 'Execution') },
  { heading: 'System',     items: simpleNav.filter((i) => i.section === 'System') },
];

const lastProjectStorageKey = 'atip.selectedProjectId';
const selectedEnvironmentStorageKey = 'atip.selectedEnvironmentId';

export function AppLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const { logout, user: auth0User } = useAuth0();
  const user = useAuthStore((s) => s.user);
  const clear = useAuthStore((s) => s.clear);
  const [darkMode, setDarkMode] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  // Global command-palette shortcut (Cmd/Ctrl+K).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setSearchOpen((o) => !o);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const isActive = (to: string) => {
    const path = location.pathname;
    if (to === '/') return path === '/';

    // On a project tab route (/projects/:id/:tab) highlight the nav item whose path segment
    // matches the tab (e.g. .../scenarios → Scenarios); fall back to Projects for tabs with no
    // dedicated nav item (overview, environments, suites…).
    const tabMatch = matchPath('/projects/:projectId/:tab', path);
    if (tabMatch?.params.tab) {
      const navForTab = `/${tabMatch.params.tab}`;
      const hasNav = simpleNav.some((i) => i.to === navForTab);
      return hasNav ? to === navForTab : to === '/projects';
    }

    // Project detail (no tab) or the projects list → highlight Projects.
    if (matchPath('/projects/:projectId', path) || path === '/projects') {
      return to === '/projects';
    }

    return path === to || path.startsWith(`${to}/`);
  };

  const matchedProjectRoute = matchPath('/projects/:projectId/*', location.pathname)
    ?? matchPath('/projects/:projectId', location.pathname);
  const routeProjectId = matchedProjectRoute?.params.projectId;
  const [selectedProjectId, setSelectedProjectId] = useState<string>(() => {
    if (typeof window === 'undefined') return '';
    return window.localStorage.getItem(lastProjectStorageKey) ?? '';
  });
  const [selectedEnvironmentId, setSelectedEnvironmentId] = useState<string>(() => {
    if (typeof window === 'undefined') return '';
    return window.localStorage.getItem(selectedEnvironmentStorageKey) ?? '';
  });

  useEffect(() => {
    if (!routeProjectId) return;
    setSelectedProjectId(routeProjectId);
    window.localStorage.setItem(lastProjectStorageKey, routeProjectId);
  }, [routeProjectId]);

  const activeProjectId = routeProjectId || selectedProjectId;

  const projectsQuery = useQuery({
    queryKey: ['projects', { page: 1, pageSize: 100 }],
    queryFn: () => listProjects({ page: 1, pageSize: 100 }),
  });

  const environmentsQuery = useQuery({
    queryKey: ['environments', activeProjectId],
    queryFn: () => listEnvironments(activeProjectId),
    enabled: Boolean(activeProjectId),
  });

  useEffect(() => {
    if (!activeProjectId) {
      setSelectedEnvironmentId('');
      window.localStorage.removeItem(selectedEnvironmentStorageKey);
      return;
    }

    const environments = environmentsQuery.data ?? [];
    if (environments.length === 0) {
      setSelectedEnvironmentId('');
      window.localStorage.removeItem(selectedEnvironmentStorageKey);
      return;
    }

    const current = environments.find((env) => env.id === selectedEnvironmentId);
    if (current) return;

    const nextEnvironmentId = environments.find((env) => env.isDefault)?.id ?? environments[0].id;
    setSelectedEnvironmentId(nextEnvironmentId);
    window.localStorage.setItem(selectedEnvironmentStorageKey, nextEnvironmentId);
  }, [activeProjectId, environmentsQuery.data, selectedEnvironmentId]);

  const selectedEnvironment = environmentsQuery.data?.find((env) => env.id === selectedEnvironmentId)
    ?? environmentsQuery.data?.find((env) => env.isDefault)
    ?? environmentsQuery.data?.[0];
  const projects = projectsQuery.data?.items ?? [];

  const handleProjectChange = (projectId: string) => {
    setSelectedProjectId(projectId);
    window.localStorage.setItem(lastProjectStorageKey, projectId);
    setSelectedEnvironmentId('');
    window.localStorage.removeItem(selectedEnvironmentStorageKey);

    if (routeProjectId) {
      navigate(`/projects/${projectId}${location.search}`, { replace: true });
    }
  };

  const handleEnvironmentChange = (environmentId: string) => {
    setSelectedEnvironmentId(environmentId);
    window.localStorage.setItem(selectedEnvironmentStorageKey, environmentId);
  };

  const handleLogout = () => {
    clear();
    logout({ logoutParams: { returnTo: `${window.location.origin}/login` } });
  };

  return (
    <div className="flex h-screen bg-gray-50 overflow-hidden">
      {/* ── Sidebar ── */}
      <aside className="w-60 flex-shrink-0 flex flex-col bg-white border-r border-border h-full">
        {/* Logo */}
        <div className="flex items-center gap-2.5 px-4 h-14 border-b border-border flex-shrink-0">
          <AtipLogo size={38} />
          <div className="leading-tight">
            <AtipWordmark size={22} />
            <div className="mt-1 text-[9px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
              AI Test Platform
            </div>
          </div>
        </div>

        {/* Nav */}
        <nav className="flex-1 overflow-y-auto px-3 py-3">
          {navSections.map((section, si) => (
            <div key={si}>
              {section.heading && (
                <p className="px-2 pt-4 pb-1 text-[10px] font-semibold text-muted-foreground uppercase tracking-widest">
                  {section.heading}
                </p>
              )}
              <div className="space-y-0.5">
                {section.items.map((item) => {
                  if (item.external) {
                    return (
                      <a
                        key={item.to + item.label}
                        href={item.to}
                        target="_blank"
                        rel="noreferrer"
                        className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors text-gray-600 hover:bg-gray-100 hover:text-gray-900"
                      >
                        <span className="flex-shrink-0 text-gray-400">{item.icon}</span>
                        {item.label}
                        <ExternalLink className="h-3 w-3 ml-auto text-gray-400" />
                      </a>
                    );
                  }
                  const active = isActive(item.to);
                  return (
                    <RouterLink
                      key={item.to + item.label}
                      to={item.to}
                      className={cn(
                        'flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors',
                        active
                          ? 'bg-violet-50 text-violet-700'
                          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
                      )}
                    >
                      <span className={cn('flex-shrink-0', active ? 'text-violet-600' : 'text-gray-400')}>
                        {item.icon}
                      </span>
                      {item.label}
                    </RouterLink>
                  );
                })}
              </div>
            </div>
          ))}
        </nav>

        {/* Bottom: dark mode */}
        <div className="flex-shrink-0 border-t border-border">
          <div className="flex items-center justify-between px-4 py-2.5">
            <span className="text-xs text-muted-foreground font-medium">Dark Mode</span>
            <button
              onClick={() => setDarkMode(!darkMode)}
              className={cn(
                'relative inline-flex h-5 w-9 cursor-pointer rounded-full border-2 border-transparent transition-colors',
                darkMode ? 'bg-violet-600' : 'bg-gray-200',
              )}
            >
              <span
                className={cn(
                  'pointer-events-none block h-4 w-4 rounded-full bg-white shadow-lg transition-transform',
                  darkMode ? 'translate-x-4' : 'translate-x-0',
                )}
              />
            </button>
          </div>
        </div>
      </aside>

      {/* ── Main area ── */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Top Header */}
        <header className="h-14 flex-shrink-0 bg-white border-b border-border flex items-center px-6 gap-4">
          <div className="flex items-center gap-3 flex-1 min-w-0 overflow-hidden">
            <div className="flex items-center gap-1.5 text-sm min-w-0">
              <span className="text-muted-foreground flex-shrink-0">Project:</span>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <button
                    className="flex items-center gap-1 font-medium text-foreground hover:text-violet-700 transition-colors min-w-0"
                    disabled={!projects.length}
                  >
                    <span className="truncate max-w-[260px]">
                      {projects.find((project) => project.id === activeProjectId)?.name ?? (projects.length ? 'Select project' : 'No projects')}
                    </span>
                    <ChevronDown className="h-3.5 w-3.5 text-muted-foreground flex-shrink-0" />
                  </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start" className="w-72">
                  <DropdownMenuLabel>Select Project</DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  {projects.map((project) => (
                    <DropdownMenuItem key={project.id} onClick={() => handleProjectChange(project.id)}>
                      <span className="truncate flex-1">{project.name}</span>
                      {project.id === activeProjectId && <span className="text-violet-600 text-xs font-semibold">Selected</span>}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
            <Separator orientation="vertical" className="h-4" />
            <div className="flex items-center gap-1.5 text-sm min-w-0">
              <span className="text-muted-foreground flex-shrink-0">Environment:</span>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <button
                    className="flex items-center gap-1 font-medium text-foreground hover:text-violet-700 transition-colors min-w-0 disabled:text-muted-foreground disabled:hover:text-muted-foreground"
                    disabled={!activeProjectId || !environmentsQuery.data?.length}
                  >
                    <span className="truncate max-w-[180px]">
                      {selectedEnvironment?.name ?? (activeProjectId ? 'No environment' : 'Select project first')}
                    </span>
                    <ChevronDown className="h-3.5 w-3.5 text-muted-foreground flex-shrink-0" />
                  </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="start" className="w-60">
                  <DropdownMenuLabel>Select Environment</DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  {(environmentsQuery.data ?? []).map((environment) => (
                    <DropdownMenuItem key={environment.id} onClick={() => handleEnvironmentChange(environment.id)}>
                      <span className="truncate flex-1">{environment.name}</span>
                      {environment.id === selectedEnvironment?.id && <span className="text-violet-600 text-xs font-semibold">Selected</span>}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>

          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              className="text-muted-foreground h-8 w-8"
              aria-label="Search"
              onClick={() => setSearchOpen(true)}
            >
              <Search className="h-4 w-4" />
            </Button>
            <NotificationBell />
            <Button
              variant="ghost"
              size="icon"
              className="text-muted-foreground h-8 w-8"
              aria-label="Help & documentation"
              onClick={() => window.open(DOCS_URL, '_blank', 'noopener,noreferrer')}
            >
              <HelpCircle className="h-4 w-4" />
            </Button>
            <Separator orientation="vertical" className="h-6 mx-1" />
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button className="flex items-center gap-2 rounded-lg px-2 py-1.5 hover:bg-gray-100 transition-colors">
                  <UserAvatar
                    name={user?.displayName}
                    email={user?.email}
                    imageUrl={auth0User?.picture}
                    size={28}
                  />
                  <div className="text-left hidden sm:block">
                    <p className="text-sm font-semibold text-foreground leading-tight">{user?.displayName ?? 'User'}</p>
                    <p className="text-[11px] text-muted-foreground leading-tight">{user?.systemRole ?? 'Admin'}</p>
                  </div>
                  <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-52">
                <DropdownMenuLabel className="font-normal">
                  <div className="flex flex-col space-y-1">
                    <p className="text-sm font-semibold">{user?.displayName}</p>
                    <p className="text-xs text-muted-foreground">{user?.email}</p>
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => navigate('/profile')}><User className="mr-2 h-4 w-4" /> Profile</DropdownMenuItem>
                <DropdownMenuItem onClick={() => navigate('/settings')}><Settings className="mr-2 h-4 w-4" /> Settings</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleLogout} className="text-destructive focus:text-destructive">
                  <LogOut className="mr-2 h-4 w-4" /> Sign out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>

      <CommandSearch
        open={searchOpen}
        onOpenChange={setSearchOpen}
        activeProjectId={activeProjectId || undefined}
        projects={projects}
      />
      <OnboardingGate />
    </div>
  );
}

