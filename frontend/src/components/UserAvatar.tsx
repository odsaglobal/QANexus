import { Avatar, AvatarFallback, AvatarImage } from './ui/avatar';
import { cn } from '../lib/utils';

/**
 * "Surya Dhal" → "SD". Falls back to the email's local part so an account that has never set a
 * display name still gets something meaningful instead of a generic glyph.
 */
export function initialsFrom(name?: string | null, email?: string | null): string {
  const source = (name?.trim() || email?.split('@')[0] || '').replace(/[._-]+/g, ' ');
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 0) return 'U';
  return parts
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase();
}

interface Props {
  name?: string | null;
  email?: string | null;
  /** Identity-provider photo. Radix falls back to the initials on its own if it fails to load. */
  imageUrl?: string | null;
  size?: number;
  className?: string;
}

/**
 * Auth0 hands out a `picture` even for users who never uploaded one: a Gravatar URL whose `d=`
 * (default) parameter redirects to `cdn.auth0.com/avatars/<xy>.png`, a grey circle with initials
 * Auth0 derived from the email local part. That renders "SU" for suryakantadhal9@gmail.com while
 * every other surface renders "SD" from the display name. Rejecting those placeholders lets our own
 * fallback run everywhere, so one person is one set of initials.
 */
export function resolveAvatarUrl(raw?: string | null): string | undefined {
  if (!raw) return undefined;
  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    return undefined;
  }
  // The placeholder itself, whether Auth0 handed it to us directly or a redirect landed on it.
  if (/(^|\.)cdn\.auth0\.com$/.test(url.hostname) && url.pathname.startsWith('/avatars/')) {
    return undefined;
  }
  // Ask Gravatar for a 404 instead of a default image. A user with a real Gravatar still gets it;
  // everyone else fails the request and drops through to the initials below.
  if (/(^|\.)gravatar\.com$/.test(url.hostname)) {
    url.searchParams.set('d', '404');
    return url.toString();
  }
  return raw;
}

/**
 * The one avatar in the app. Every surface used to roll its own — the top bar rendered initials and
 * never the photo, the profile page rendered the photo, and the admin table used a bare span with a
 * third initials implementation — so the same person appeared three different ways on three screens.
 *
 * Size is a number rather than a Tailwind sizing class because the initials have to scale with the
 * circle; a class can set the box but can't tell the fallback what type size to use.
 */
export function UserAvatar({ name, email, imageUrl, size = 32, className }: Props) {
  const src = resolveAvatarUrl(imageUrl);
  return (
    <Avatar className={cn('shrink-0', className)} style={{ height: size, width: size }}>
      {src && (
        // Google's CDN serves 403s for some referrers, which would silently drop everyone back to
        // initials; sending no referrer keeps the photo.
        <AvatarImage src={src} alt={name ?? 'User avatar'} referrerPolicy="no-referrer" />
      )}
      <AvatarFallback
        className="bg-violet-100 font-semibold text-violet-700"
        style={{ fontSize: Math.round(size * 0.38) }}
      >
        {initialsFrom(name, email)}
      </AvatarFallback>
    </Avatar>
  );
}
