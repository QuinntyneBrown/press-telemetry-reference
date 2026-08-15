import { test as base } from '@playwright/test';
import { TelemetryApiMock } from './support/telemetry-api-mock';
import { SignalRHubMock } from './support/signalr-hub-mock';
import { DashboardPage } from './pages/dashboard-page';
import { SeriesDetailPage } from './pages/series-detail-page';
import { T0 } from './support/time';

interface Fixtures {
  /** Opt in per spec file for deterministic timers/Date.now (never in layout/resize specs — it captures rAF). */
  fakeClock: boolean;
  api: TelemetryApiMock;
  hub: SignalRHubMock;
  dashboard: DashboardPage;
  seriesDetail: SeriesDetailPage;
}

export const test = base.extend<Fixtures>({
  fakeClock: [false, { option: true }],
  // Auto-ticking fake clock from T0. Never pause before load — the app boot
  // path needs ticking timers and renders a blank page under a paused clock.
  // Specs that need frozen time call freezeAt(page, seconds) AFTER hydration.
  page: async ({ page, fakeClock }, use) => {
    if (fakeClock) await page.clock.install({ time: T0 });
    await use(page);
  },
  api: async ({ page }, use) => {
    const api = new TelemetryApiMock(page);
    await api.install();
    await use(api);
  },
  hub: async ({ page }, use) => {
    const hub = new SignalRHubMock();
    await hub.install(page);
    await use(hub);
  },
  // Depends on api/hub so the network mocks are always installed before a test
  // navigates, even when the test doesn't reference them directly (fixtures are lazy).
  dashboard: async ({ page, api, hub }, use) => {
    void api;
    void hub;
    await use(new DashboardPage(page));
  },
  seriesDetail: async ({ page, api, hub }, use) => {
    void api;
    void hub;
    await use(new SeriesDetailPage(page));
  },
});

export { expect } from '@playwright/test';
