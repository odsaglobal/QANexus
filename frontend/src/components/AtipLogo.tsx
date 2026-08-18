import atipLogoSymbolSrc from '../assets/atip-logo-symbol.png';

export function AtipLogo({ size = 36 }: { size?: number }) {
  return (
    <img
      src={atipLogoSymbolSrc}
      alt="ATiP logo"
      style={{ height: size, width: 'auto', display: 'block' }}
    />
  );
}

/** Full horizontal lockup: symbol image + text rendered in code. */
export function AtipLogoFull({ size = 36 }: { size?: number }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: Math.max(8, Math.round(size * 0.16)) }}>
      <AtipLogo size={size} />
      <div style={{ lineHeight: 1 }}>
        <div
          style={{
            fontSize: Math.round(size * 0.76),
            fontWeight: 800,
            letterSpacing: '0.01em',
            color: '#1f2a44',
          }}
        >
          ATiP
        </div>
        <div
          style={{
            marginTop: 4,
            fontSize: Math.max(9, Math.round(size * 0.2)),
            fontWeight: 600,
            letterSpacing: '0.1em',
            textTransform: 'uppercase',
            color: '#6b7280',
          }}
        >
          Autonomous Test Intelligence Platform
        </div>
      </div>
    </div>
  );
}
