import { Suspense, type ReactNode } from 'react';

export function ViewLoader({ children }: { children: ReactNode }) {
  return <Suspense fallback={null}>{children}</Suspense>;
}
