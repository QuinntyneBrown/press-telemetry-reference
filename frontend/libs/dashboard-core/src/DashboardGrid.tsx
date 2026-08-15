import type { ReactNode } from 'react';

export interface DashboardGridProps {
  children: ReactNode;
  /** True while the grid shows loading placeholders (renders aria-busy). */
  busy?: boolean;
}

export function DashboardGrid({ children, busy }: DashboardGridProps) {
  return (
    <div className="grid" data-testid="dashboard-grid" aria-busy={busy || undefined}>
      {children}
    </div>
  );
}
