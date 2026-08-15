import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@press/dashboard-core';
import { App } from './App';

// staleTime/refetchOnWindowFocus/retry are load-bearing: live data arrives by
// patching this cache (L2-010); background refetches would violate L2-009 AC3.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: Infinity,
      refetchOnWindowFocus: false,
      retry: false,
    },
  },
});

// No StrictMode: its dev-only double-mount would open the hub connection twice
// per view mount, defeating deterministic connection-lifecycle behaviour (L2-012).
createRoot(document.getElementById('root')!).render(
  <QueryClientProvider client={queryClient}>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </QueryClientProvider>,
);
