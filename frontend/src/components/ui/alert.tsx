import * as React from 'react';
import { cn } from '../../lib/utils';
import { AlertTriangle, CheckCircle, Info, XCircle } from 'lucide-react';

interface AlertProps extends React.HTMLAttributes<HTMLDivElement> {
  severity?: 'error' | 'warning' | 'info' | 'success';
}

const icons = {
  error: XCircle,
  warning: AlertTriangle,
  info: Info,
  success: CheckCircle,
};

const styles = {
  error: 'bg-red-50 border-red-200 text-red-800',
  warning: 'bg-amber-50 border-amber-200 text-amber-800',
  info: 'bg-blue-50 border-blue-200 text-blue-800',
  success: 'bg-green-50 border-green-200 text-green-800',
};

const iconStyles = {
  error: 'text-red-500',
  warning: 'text-amber-500',
  info: 'text-blue-500',
  success: 'text-green-500',
};

function Alert({ className, severity = 'info', children, ...props }: AlertProps) {
  const Icon = icons[severity];
  return (
    <div
      role="alert"
      className={cn(
        'flex items-start gap-3 rounded-lg border p-3 text-sm',
        styles[severity],
        className,
      )}
      {...props}
    >
      <Icon className={cn('mt-0.5 h-4 w-4 shrink-0', iconStyles[severity])} />
      <div>{children}</div>
    </div>
  );
}

const AlertTitle = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, ...props }, ref) => (
    <h5 ref={ref} className={cn('mb-1 font-semibold leading-none tracking-tight', className)} {...props} />
  ),
);
AlertTitle.displayName = 'AlertTitle';

const AlertDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('text-sm [&_p]:leading-relaxed', className)} {...props} />
  ),
);
AlertDescription.displayName = 'AlertDescription';

export { Alert, AlertTitle, AlertDescription };
