import symbol from '../assets/atip-logo-symbol.png';

/**
 * ATiP brand mark — the gradient triangle from the master logo artwork.
 *
 * The symbol-only export is used rather than the lockup with text because every surface pairs the
 * mark with `AtipWordmark`, which can be sized and recoloured independently (the raster wordmark
 * is fixed dark navy and disappears on the auth panel).
 */
export function AtipLogo({ size = 36 }: { size?: number }) {
  return (
    <img
      src={symbol}
      width={size}
      height={size}
      alt="ATiP logo"
      style={{ display: 'block', flexShrink: 0, objectFit: 'contain' }}
    />
  );
}

/**
 * "ATiP" wordmark. Kept as text rather than a second image so it inherits the page font and stays
 * crisp in the printed run report.
 *
 * The lowercase "i" is part of the brand spelling, not a typo.
 */
export function AtipWordmark({ size = 22, onDark = false }: { size?: number; onDark?: boolean }) {
  return (
    <span
      style={{
        fontSize: size,
        lineHeight: 1,
        letterSpacing: '-0.01em',
        whiteSpace: 'nowrap',
        display: 'inline-block',
        fontWeight: 800,
        color: onDark ? 'rgba(255,255,255,0.95)' : '#1f2a44',
      }}
    >
      ATiP
    </span>
  );
}
