import * as React from 'react';
import { AlertTriangle, Info } from 'lucide-react';
import { Button } from './button';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from './dialog';

type DialogKind = 'confirm' | 'alert';
type DialogTone = 'default' | 'destructive';

export interface ConfirmOptions {
  title?: string;
  description?: React.ReactNode;
  confirmText?: string;
  cancelText?: string;
  tone?: DialogTone;
}

export interface AlertOptions {
  title?: string;
  description?: React.ReactNode;
  confirmText?: string;
  tone?: DialogTone;
}

interface DialogState extends ConfirmOptions {
  kind: DialogKind;
  open: boolean;
  resolve?: (value: boolean) => void;
}

interface ConfirmContextValue {
  confirm: (options: string | ConfirmOptions) => Promise<boolean>;
  alert: (options: string | AlertOptions) => Promise<void>;
}

const ConfirmContext = React.createContext<ConfirmContextValue | null>(null);

const initialState: DialogState = {
  kind: 'confirm',
  open: false,
  title: '',
  description: '',
};

export function ConfirmProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = React.useState<DialogState>(initialState);

  const close = React.useCallback((result: boolean) => {
    setState((prev) => {
      prev.resolve?.(result);
      return { ...prev, open: false, resolve: undefined };
    });
  }, []);

  const confirm = React.useCallback((options: string | ConfirmOptions) => {
    const opts = typeof options === 'string' ? { description: options } : options;
    return new Promise<boolean>((resolve) => {
      setState({
        kind: 'confirm',
        open: true,
        title: opts.title ?? 'Are you sure?',
        description: opts.description ?? '',
        confirmText: opts.confirmText ?? 'Confirm',
        cancelText: opts.cancelText ?? 'Cancel',
        tone: opts.tone ?? 'default',
        resolve,
      });
    });
  }, []);

  const alert = React.useCallback((options: string | AlertOptions) => {
    const opts = typeof options === 'string' ? { description: options } : options;
    return new Promise<void>((resolve) => {
      setState({
        kind: 'alert',
        open: true,
        title: opts.title ?? 'Notice',
        description: opts.description ?? '',
        confirmText: opts.confirmText ?? 'OK',
        tone: opts.tone ?? 'default',
        resolve: () => resolve(),
      });
    });
  }, []);

  const value = React.useMemo(() => ({ confirm, alert }), [confirm, alert]);

  const isDestructive = state.tone === 'destructive';
  const Icon = isDestructive ? AlertTriangle : Info;

  return (
    <ConfirmContext.Provider value={value}>
      {children}
      <Dialog open={state.open} onOpenChange={(open) => { if (!open) close(false); }}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <div className="flex items-start gap-3">
              <span
                className={
                  'mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full ' +
                  (isDestructive ? 'bg-destructive/10 text-destructive' : 'bg-primary/10 text-primary')
                }
              >
                <Icon className="h-5 w-5" />
              </span>
              <div className="space-y-1.5">
                <DialogTitle>{state.title}</DialogTitle>
                {state.description ? (
                  <DialogDescription>{state.description}</DialogDescription>
                ) : null}
              </div>
            </div>
          </DialogHeader>
          <DialogFooter>
            {state.kind === 'confirm' && (
              <Button type="button" variant="outline" onClick={() => close(false)}>
                {state.cancelText}
              </Button>
            )}
            <Button
              type="button"
              variant={isDestructive ? 'destructive' : 'default'}
              onClick={() => close(true)}
              autoFocus
            >
              {state.confirmText}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </ConfirmContext.Provider>
  );
}

export function useConfirm(): ConfirmContextValue {
  const context = React.useContext(ConfirmContext);
  if (!context) {
    throw new Error('useConfirm must be used within a ConfirmProvider');
  }
  return context;
}
