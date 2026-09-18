import { useEffect, useState } from 'react';
import { BarChart3, Check, FileText, PlayCircle, Radar, Sparkles } from 'lucide-react';
import { AtipLogo, AtipWordmark } from './AtipLogo';

/**
 * The five stages a run actually passes through, in order. The panel animates them rather than
 * listing features because the pipeline *is* the pitch: the product's value is that these steps
 * happen without a human in between.
 */
const STAGES = [
  { icon: FileText, label: 'Requirements ingested', desc: 'SRS, BRD and Swagger parsed into modules, features and stories.' },
  { icon: Radar, label: 'Application explored', desc: 'A browser agent maps every page, element and resilient locator.' },
  { icon: Sparkles, label: 'Scenarios generated', desc: 'Positive, negative, boundary and security cases — written for you.' },
  { icon: PlayCircle, label: 'Suite executed', desc: 'Deterministic runs that self-heal when a locator drifts.' },
  { icon: BarChart3, label: 'Report published', desc: 'Pass rates, verdicts and screenshot evidence for every step.' },
];

const LAST = STAGES.length - 1;

/** Someone who has asked the OS to stop moving things should not be handed a looping animation. */
function usePrefersReducedMotion() {
  const [reduced, setReduced] = useState(
    () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false,
  );
  useEffect(() => {
    const query = window.matchMedia('(prefers-reduced-motion: reduce)');
    const onChange = () => setReduced(query.matches);
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, []);
  return reduced;
}

function WorkflowPipeline() {
  const reducedMotion = usePrefersReducedMotion();
  const [active, setActive] = useState(0);

  // A timeout per stage rather than one interval, so the finished pipeline can hold a beat longer
  // before it loops — the completed state is the payoff, not a frame to rush past.
  useEffect(() => {
    if (reducedMotion) return;
    const dwell = active === LAST ? 2600 : 1700;
    const timer = setTimeout(() => setActive((i) => (i + 1) % STAGES.length), dwell);
    return () => clearTimeout(timer);
  }, [active, reducedMotion]);

  // Reduced motion still gets the whole story, just told at once instead of over ten seconds.
  const settled = reducedMotion;
  const wrapping = active === 0 && !settled;

  return (
    <ol className="relative">
      {STAGES.map((stage, i) => {
        const done = settled || i < active;
        const current = !settled && i === active;
        const Icon = stage.icon;

        return (
          <li key={stage.label} className="flex gap-4">
            {/* Rail column: the node, plus the connector that fills as the run progresses. */}
            <div className="flex w-10 flex-shrink-0 flex-col items-center">
              <div
                className={[
                  'flex h-10 w-10 items-center justify-center rounded-xl border backdrop-blur transition-all duration-500',
                  current
                    ? 'scale-110 border-emerald-300/60 bg-emerald-400/15 text-emerald-200 shadow-[0_0_28px_-6px_rgba(52,211,153,0.9)]'
                    : done
                      ? 'border-emerald-400/30 bg-emerald-400/10 text-emerald-300/90'
                      : 'border-white/10 bg-white/[0.04] text-violet-200/40',
                ].join(' ')}
              >
                {done ? <Check className="h-5 w-5" strokeWidth={2.5} /> : <Icon className="h-5 w-5" />}
              </div>

              {i < LAST && (
                <div className="relative my-1.5 w-px flex-1 bg-white/10">
                  <div
                    className="absolute inset-x-0 top-0 bg-gradient-to-b from-emerald-400/70 to-emerald-300"
                    style={{
                      height: done ? '100%' : '0%',
                      // Looping back to stage one would otherwise drain five rails at once, which
                      // reads as the pipeline running backwards. Snap them instead.
                      transition: wrapping ? 'none' : 'height 700ms cubic-bezier(0.4, 0, 0.2, 1)',
                    }}
                  />
                </div>
              )}
            </div>

            <div className={`pb-7 transition-opacity duration-500 ${current || done ? 'opacity-100' : 'opacity-45'}`}>
              <p
                className={`text-sm font-semibold transition-colors duration-500 ${
                  current ? 'text-white' : 'text-violet-100'
                }`}
              >
                {stage.label}
              </p>
              <p className="mt-0.5 max-w-xs text-xs leading-relaxed text-violet-300">{stage.desc}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

export function AuthBrandPanel() {
  return (
    <div className="relative hidden flex-1 flex-col justify-center overflow-hidden bg-gradient-to-br from-violet-950 via-violet-900 to-indigo-950 p-12 md:flex lg:p-16">
      {/* Background decoration */}
      <div
        className="absolute inset-0 opacity-20"
        style={{
          backgroundImage:
            'radial-gradient(circle at 20% 80%, #7c3aed 0%, transparent 50%), radial-gradient(circle at 80% 20%, #4f46e5 0%, transparent 50%)',
        }}
      />
      <div
        className="absolute inset-0"
        style={{
          backgroundImage: 'radial-gradient(rgba(255,255,255,0.04) 1px, transparent 1px)',
          backgroundSize: '28px 28px',
        }}
      />
      {/* Fades the grid out towards the sign-in half so the two sides meet softly. */}
      <div className="absolute inset-0 bg-gradient-to-r from-transparent via-transparent to-violet-950/60" />

      <div className="relative z-10 flex max-h-full flex-col">
        <div className="flex items-center gap-3">
          <AtipLogo size={44} />
          <div className="leading-tight">
            <AtipWordmark size={30} onDark />
            <div className="mt-1.5 text-[10px] font-semibold uppercase tracking-[0.18em] text-violet-300">
              Autonomous Test Intelligence Platform
            </div>
          </div>
        </div>

        <h1 className="mb-3 mt-12 text-[2.1rem] font-bold leading-tight text-white">
          From requirements to evidence,
          <br />
          <span className="text-emerald-300">without writing a test.</span>
        </h1>
        <p className="mb-10 max-w-sm text-sm leading-relaxed text-violet-200">
          Enterprise-grade AI that reads your documentation, explores your application, and keeps the
          regression suite honest on every release.
        </p>

        <p className="mb-6 text-[10px] font-semibold uppercase tracking-[0.18em] text-violet-300/70">
          The pipeline, end to end
        </p>
        <WorkflowPipeline />

        <p className="mt-6 text-xs text-violet-400">
          © {new Date().getFullYear()} ATiP. All rights reserved.
        </p>
      </div>
    </div>
  );
}

