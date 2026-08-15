import { Suspense, type ReactNode } from 'react';
import '../view-loader.css';

function ViewLoadingFallback() {
  return (
    <main className="main">
      <div className="view-loading">
        <div className="spinner" role="status" aria-label="Loading view" />
        <p className="view-loading__text">Loading view…</p>
        <p className="view-loading__chunk">telemetry-dashboard-view</p>
      </div>
    </main>
  );
}

export function ViewLoader({ children }: { children: ReactNode }) {
  return <Suspense fallback={<ViewLoadingFallback />}>{children}</Suspense>;
}
