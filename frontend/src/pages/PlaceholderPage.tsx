import { Rocket } from 'lucide-react';
import { Card, CardContent } from '../components/ui/card';

export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">{title}</h1>
        <p className="text-muted-foreground text-sm mt-0.5">
          Part of the ATiP intelligent testing pipeline.
        </p>
      </div>
      <Card>
        <CardContent className="py-20 text-center">
          <div className="inline-flex h-16 w-16 rounded-2xl bg-violet-50 border border-violet-100 items-center justify-center mb-5">
            <Rocket className="h-8 w-8 text-violet-500" />
          </div>
          <h2 className="text-lg font-semibold text-foreground mb-2">Roadmap — coming soon</h2>
          <p className="text-muted-foreground text-sm max-w-md mx-auto">
            <strong className="text-violet-600">{title}</strong> is on the ATiP roadmap.
            The foundation, Requirement Intelligence, Scenario Generation, Knowledge Graph and
            Application Explorer are live and ready.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
