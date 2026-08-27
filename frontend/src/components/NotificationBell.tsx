import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Bell, CheckCheck, Loader2, CheckCircle2, XCircle, AlertTriangle, Info } from 'lucide-react';
import {
  listNotifications, getUnreadCount, markNotificationRead, markAllNotificationsRead,
} from '../api/notifications';
import { Button } from './ui/button';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuTrigger,
} from './ui/dropdown-menu';
import { cn } from '../lib/utils';

function levelIcon(level: string) {
  switch (level) {
    case 'success': return <CheckCircle2 className="h-4 w-4 text-green-600" />;
    case 'error': return <XCircle className="h-4 w-4 text-red-600" />;
    case 'warning': return <AlertTriangle className="h-4 w-4 text-amber-600" />;
    default: return <Info className="h-4 w-4 text-blue-600" />;
  }
}

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60000);
  if (m < 1) return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}

export function NotificationBell() {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);

  const { data: unread = 0 } = useQuery({
    queryKey: ['notifications-unread'],
    queryFn: getUnreadCount,
    refetchInterval: 20000,
  });

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['notifications'],
    queryFn: () => listNotifications(30),
    enabled: open,
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['notifications'] });
    queryClient.invalidateQueries({ queryKey: ['notifications-unread'] });
  };

  const markOne = useMutation({ mutationFn: markNotificationRead, onSuccess: invalidate });
  const markAll = useMutation({ mutationFn: markAllNotificationsRead, onSuccess: invalidate });

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="text-muted-foreground h-8 w-8 relative" aria-label="Notifications">
          <Bell className="h-4 w-4" />
          {unread > 0 && (
            <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 px-1 rounded-full bg-violet-600 text-white text-[10px] font-semibold flex items-center justify-center">
              {unread > 99 ? '99+' : unread}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-96 p-0">
        <div className="flex items-center justify-between px-3 py-2 border-b border-border">
          <span className="text-sm font-semibold">Notifications</span>
          <Button
            variant="ghost" size="sm" className="h-7 text-xs text-violet-600"
            disabled={markAll.isPending || unread === 0}
            onClick={() => markAll.mutate()}
          >
            <CheckCheck className="h-3.5 w-3.5 mr-1" /> Mark all read
          </Button>
        </div>
        <div className="max-h-[420px] overflow-y-auto">
          {isLoading ? (
            <div className="py-8 text-center text-muted-foreground text-sm">
              <Loader2 className="h-4 w-4 animate-spin mx-auto mb-1" /> Loading…
            </div>
          ) : items.length === 0 ? (
            <p className="py-10 text-center text-sm text-muted-foreground">No notifications yet.</p>
          ) : (
            items.map((n) => (
              <button
                type="button"
                key={n.id}
                onClick={() => { if (!n.isRead) markOne.mutate(n.id); }}
                className={cn(
                  'flex w-full items-start gap-2.5 px-3 py-2.5 text-left border-b border-border last:border-0 hover:bg-muted transition-colors',
                  !n.isRead && 'bg-violet-50/60',
                )}
              >
                <span className="mt-0.5 shrink-0">{levelIcon(n.level)}</span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="text-sm font-medium text-foreground truncate">{n.title}</span>
                    {!n.isRead && <span className="h-1.5 w-1.5 rounded-full bg-violet-600 shrink-0" />}
                  </span>
                  <span className="block text-xs text-muted-foreground mt-0.5 line-clamp-2">{n.message}</span>
                  <span className="block text-[10px] text-muted-foreground mt-1">{timeAgo(n.createdAtUtc)}</span>
                </span>
              </button>
            ))
          )}
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
