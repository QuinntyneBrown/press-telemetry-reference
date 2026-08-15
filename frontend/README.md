# Press Telemetry — Frontend

React dashboard for the press telemetry reference solution. Renders historical and live
telemetry in a responsive, dark-mode UI (specs: `docs/specs/L2.md` L2-009…L2-016; designs:
`docs/detailed-designs/dashboard/`; visual contract: `docs/mocks/`).

## Workspace

npm workspaces + Vite + React + TypeScript. Each library exposes a single public entry
point (`package.json` `exports` → `src/index.ts`); deep imports are rejected by the
`no-restricted-imports` ESLint rule (L2-014).

| Package | Purpose |
| --- | --- |
| `apps/dashboard` | App shell: router, `React.lazy` view loading, Suspense fallback, Query provider |
| `libs/dashboard-core` | Props-only presentation components + `theme.css` (verbatim copy of `docs/mocks/assets/tokens.css`) — no REST/SignalR/Query code (L2-013) |
| `libs/telemetry-dashboard-view` | Both views (`TelemetryDashboardView`, `TelemetrySeriesDetailView`) plus the data layer (TanStack Query hooks, hub client, ~1 Hz batcher, cache patching) — loaded as one on-demand chunk |
| `e2e` | Playwright acceptance tests (Page Object Model), fully mocked network |

## Getting started

```
npm install
npx playwright install chromium
npm run dev          # Vite on http://localhost:5173
npm run e2e          # acceptance suite (attaches to a running dev server)
npm run e2e:ui       # watch mode — the ATDD loop
npm run typecheck && npm run lint && npm run build
```

The dev server proxies `/api` and `/hubs` (websocket) to the backend at
`http://localhost:5063`. `VITE_API_BASE_URL` overrides the API origin (L2-018);
the committed default is same-origin.

## Testing approach (ATDD)

Every behaviour was driven by a failing Playwright test first; test titles carry the
requirement tag (`L2-010 AC1 …`) matching the backend's convention. The e2e suite mocks
the entire network inside the browser: `page.route()` serves the pinned REST contract and
`page.routeWebSocket()` speaks the SignalR JSON hub protocol (handshake `{}`, pings,
`telemetry` invocations, close-with-`allowReconnect` for reconnect tests). Timing-sensitive
specs use Playwright's fake clock — installed auto-ticking before load, frozen only after
hydration (a clock paused during boot blanks the app), never in layout/resize specs (the
fake clock captures rAF).

Deliberately untested (architecture, per project constraint): workspace-shape inspection
(L2-013 AC1–3), chunk/lint inspection (L2-014 AC1–2 — enforced by the ESLint rule and
verified manually on `npm run build`), and theme-token audits (L2-015).

## Decisions & trade-offs

- **SignalR `skipNegotiation: true` + WebSockets-only transport** — one fewer round-trip
  and a single interception point for tests; the trade-off is no SSE/long-polling fallback.
- **Indefinite reconnect** — constant 2 s retry delay via `withAutomaticReconnect` (the
  library default stops after four attempts) plus a retry loop around the initial
  `start()`, which automatic reconnect does not cover (L2-012).
- **No React StrictMode** — its dev-only double-mount opens the hub connection twice per
  view mount, defeating deterministic connection lifecycle behaviour and tests.
- **Hand-rolled SVG charts** — `viewBox="0 0 100 100"` + `preserveAspectRatio="none"` with
  non-scaling strokes and HTML axis labels, exactly as the mocks prescribe; no chart library.
- **Query defaults are load-bearing** — `staleTime: Infinity`, `refetchOnWindowFocus: false`,
  `retry: false`: live data arrives by patching the cache (L2-010), so background refetches
  would violate L2-009 AC3.
- **Units are client metadata** — the API carries no unit field; `units.ts` maps metric →
  unit for display.
