import { AtipLogo, AtipWordmark } from './AtipLogo';

/**
 * Full-screen boot splash for the moments the app has nothing to show yet: the Auth0 redirect
 * callback, and the session restore on a cold load. Both routes share it so the hand-off from one
 * to the other doesn't flash a second, differently-styled loader.
 */
export function AppSplash({ message = 'Preparing your workspace…' }: { message?: string }) {
  return (
    <div className="flex h-screen flex-col items-center justify-center gap-7 bg-white">
      <div className="relative flex flex-col items-center gap-3">
        {/* Soft halo behind the mark — carries the brand without animating the logo itself. */}
        <div aria-hidden className="absolute -inset-10 rounded-full bg-violet-500/10 blur-3xl" />
        <div className="relative">
          <AtipLogo size={56} />
        </div>
        <div className="relative">
          <AtipWordmark size={26} />
        </div>
      </div>

      <div
        className="h-[3px] w-40 overflow-hidden rounded-full bg-slate-200"
        role="progressbar"
        aria-label={message}
      >
        <div className="animate-indeterminate h-full w-1/3 rounded-full bg-gradient-to-r from-violet-500 to-emerald-400" />
      </div>

      <p className="text-xs text-muted-foreground">{message}</p>
    </div>
  );
}
