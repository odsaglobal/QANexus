import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { useQuery } from '@tanstack/react-query';
import {
  Search, FolderOpen, FlaskConical, CornerDownLeft, ArrowUp, ArrowDown, type LucideIcon,
  LayoutDashboard, Server, FileText, Compass, Play, BarChart2, Lightbulb, Activity, Settings, Shield,
} from 'lucide-react';
import { listScenarios } from '../api/scenarios';
import { cn } from '../lib/utils';

interface SearchProject {
  id: string;
  name: string;
}

interface ResultItem {
  key: string;
  group: 'Pages' | 'Projects' | 'Scenarios';
  label: string;
  sublabel?: string;
  icon: LucideIcon;
  run: () => void;
}

const pages: { label: string; to: string; icon: LucideIcon }[] = [
  { label: 'Dashboard', to: '/', icon: LayoutDashboard },
  { label: 'Projects', to: '/projects', icon: FolderOpen },
  { label: 'Environments', to: '/environments', icon: Server },
  { label: 'Business Context', to: '/requirements', icon: FileText },
  { label: 'Scenarios', to: '/scenarios', icon: FlaskConical },
  { label: 'Explorer', to: '/explorer', icon: Compass },
  { label: 'Executions', to: '/executions', icon: Play },
  { label: 'Reports', to: '/reports', icon: BarChart2 },
  { label: 'AI Insights', to: '/ai-insights', icon: Lightbulb },
  { label: 'Agent Monitor', to: '/agents', icon: Activity },
  { label: 'Settings', to: '/settings', icon: Settings },
  { label: 'Administration', to: '/administration', icon: Shield },
];

export function CommandSearch({
  open,
  onOpenChange,
  activeProjectId,
  projects,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  activeProjectId?: string;
  projects: SearchProject[];
}) {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const q = query.trim().toLowerCase();

  const scenariosQuery = useQuery({
    queryKey: ['search-scenarios', activeProjectId],
    queryFn: () => listScenarios(activeProjectId as string),
    enabled: open && Boolean(activeProjectId),
    staleTime: 30_000,
  });

  // Reset transient state each time the palette opens.
  useEffect(() => {
    if (open) {
      setQuery('');
      setSelected(0);
    }
  }, [open]);

  const close = () => onOpenChange(false);

  const results = useMemo<ResultItem[]>(() => {
    const items: ResultItem[] = [];

    for (const page of pages) {
      if (!q || page.label.toLowerCase().includes(q)) {
        items.push({
          key: `page-${page.to}`,
          group: 'Pages',
          label: page.label,
          icon: page.icon,
          run: () => navigate(page.to),
        });
      }
    }

    for (const project of projects) {
      if (!q || project.name.toLowerCase().includes(q)) {
        items.push({
          key: `project-${project.id}`,
          group: 'Projects',
          label: project.name,
          sublabel: 'Open project',
          icon: FolderOpen,
          run: () => navigate(`/projects/${project.id}`),
        });
      }
    }

    if (activeProjectId) {
      for (const scenario of scenariosQuery.data ?? []) {
        if (!q || scenario.title.toLowerCase().includes(q)) {
          items.push({
            key: `scenario-${scenario.id}`,
            group: 'Scenarios',
            label: scenario.title,
            sublabel: `${scenario.type} · ${scenario.priority}`,
            icon: FlaskConical,
            run: () => navigate(`/projects/${activeProjectId}/scenarios`),
          });
        }
      }
    }

    return items;
  }, [q, projects, activeProjectId, scenariosQuery.data, navigate]);

  // Limit to keep the list snappy.
  const limited = results.slice(0, 40);

  useEffect(() => {
    setSelected((s) => (s >= limited.length ? 0 : s));
  }, [limited.length]);

  const activate = (item: ResultItem | undefined) => {
    if (!item) return;
    item.run();
    close();
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelected((s) => Math.min(limited.length - 1, s + 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelected((s) => Math.max(0, s - 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      activate(limited[selected]);
    }
  };

  // Keep the highlighted row scrolled into view.
  useEffect(() => {
    const el = listRef.current?.querySelector<HTMLElement>(`[data-index="${selected}"]`);
    el?.scrollIntoView({ block: 'nearest' });
  }, [selected]);

  let lastGroup: string | null = null;

  return (
    <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <DialogPrimitive.Content
          onOpenAutoFocus={(e) => {
            e.preventDefault();
            inputRef.current?.focus();
          }}
          aria-label="Search"
          className="fixed left-[50%] top-[12%] z-50 w-full max-w-xl translate-x-[-50%] overflow-hidden rounded-xl border border-border bg-background shadow-2xl data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
        >
          <DialogPrimitive.Title className="sr-only">Search</DialogPrimitive.Title>
          {/* Input */}
          <div className="flex items-center gap-2.5 border-b border-border px-4">
            <Search className="h-4 w-4 flex-shrink-0 text-muted-foreground" />
            <input
              ref={inputRef}
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setSelected(0);
              }}
              onKeyDown={onKeyDown}
              placeholder="Search pages, projects, scenarios…"
              className="h-12 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            />
          </div>

          {/* Results */}
          <div ref={listRef} className="max-h-[60vh] overflow-y-auto p-2">
            {limited.length === 0 ? (
              <p className="px-3 py-8 text-center text-sm text-muted-foreground">
                No results {q ? `for “${query}”` : ''}
              </p>
            ) : (
              limited.map((item, index) => {
                const Icon = item.icon;
                const showHeading = item.group !== lastGroup;
                lastGroup = item.group;
                return (
                  <div key={item.key}>
                    {showHeading && (
                      <p className="px-2 pb-1 pt-3 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground">
                        {item.group}
                      </p>
                    )}
                    <button
                      data-index={index}
                      onMouseMove={() => setSelected(index)}
                      onClick={() => activate(item)}
                      className={cn(
                        'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm transition-colors',
                        index === selected ? 'bg-violet-50 text-violet-800' : 'text-foreground hover:bg-gray-100',
                      )}
                    >
                      <Icon
                        className={cn(
                          'h-4 w-4 flex-shrink-0',
                          index === selected ? 'text-violet-600' : 'text-gray-400',
                        )}
                      />
                      <span className="flex-1 truncate">{item.label}</span>
                      {item.sublabel && (
                        <span className="flex-shrink-0 text-xs text-muted-foreground">{item.sublabel}</span>
                      )}
                    </button>
                  </div>
                );
              })
            )}
          </div>

          {/* Footer hint */}
          <div className="flex items-center gap-4 border-t border-border px-4 py-2 text-[11px] text-muted-foreground">
            <span className="flex items-center gap-1">
              <ArrowUp className="h-3 w-3" />
              <ArrowDown className="h-3 w-3" />
              to navigate
            </span>
            <span className="flex items-center gap-1">
              <CornerDownLeft className="h-3 w-3" />
              to open
            </span>
            <span className="ml-auto">esc to close</span>
          </div>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
