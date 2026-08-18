import { Brain, Globe, Sparkles } from 'lucide-react';
import { AtipLogo } from './AtipLogo';

const features = [
  { icon: Brain, label: 'AI understands your docs', desc: 'Upload SRS, BRD, Swagger — AI extracts every module, feature and user story automatically.' },
  { icon: Globe, label: 'Autonomous exploration', desc: 'Playwright-powered browser agent crawls your app, capturing pages, elements and locators.' },
  { icon: Sparkles, label: 'Zero-code test generation', desc: 'Positive, negative, boundary and security scenarios — generated, not written.' },
];

export function AuthBrandPanel() {
  return (
    <div className="hidden md:flex flex-1 flex-col justify-center relative overflow-hidden bg-gradient-to-br from-violet-950 via-violet-900 to-indigo-900 p-12 lg:p-16">
      {/* Background decoration */}
      <div className="absolute inset-0 opacity-20"
        style={{
          backgroundImage: 'radial-gradient(circle at 20% 80%, #7c3aed 0%, transparent 50%), radial-gradient(circle at 80% 20%, #4f46e5 0%, transparent 50%)',
        }}
      />
      <div className="absolute inset-0"
        style={{
          backgroundImage: 'radial-gradient(rgba(255,255,255,0.04) 1px, transparent 1px)',
          backgroundSize: '28px 28px',
        }}
      />

      <div className="relative z-10">
        {/* Logo */}
        <div className="mb-12 flex items-center gap-3">
          <AtipLogo size={48} />
          <div className="leading-tight">
            <div className="text-[34px] font-extrabold tracking-tight text-white">ATiP</div>
            <div className="text-[10px] font-semibold uppercase tracking-[0.18em] text-violet-200">
              Autonomous Test Intelligence Platform
            </div>
          </div>
        </div>

        <h1 className="text-4xl font-bold text-white leading-tight mb-4">
          Intelligent Testing,<br />
          <span className="text-violet-300">Autonomously Powered</span>
        </h1>
        <p className="text-violet-200 text-base mb-12 max-w-sm leading-relaxed">
          Enterprise-grade AI that understands your requirements, explores your app, and generates comprehensive test suites automatically.
        </p>

        <div className="space-y-6">
          {features.map(({ icon: Icon, label, desc }) => (
            <div key={label} className="flex items-start gap-4">
              <div className="flex-shrink-0 h-10 w-10 rounded-lg bg-white/10 backdrop-blur flex items-center justify-center border border-white/20">
                <Icon className="h-5 w-5 text-violet-200" />
              </div>
              <div>
                <p className="text-white font-semibold text-sm mb-0.5">{label}</p>
                <p className="text-violet-300 text-xs leading-relaxed">{desc}</p>
              </div>
            </div>
          ))}
        </div>

        <p className="mt-12 text-violet-400 text-xs">© 2025 ATiP. All rights reserved.</p>
      </div>
    </div>
  );
}

