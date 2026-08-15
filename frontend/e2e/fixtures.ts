import { test as base } from '@playwright/test';
import { TelemetryApiMock } from './support/telemetry-api-mock';
import { SignalRHubMock } from './support/signalr-hub-mock';
import { DashboardPage } from './pages/dashboard-page';
import { T0 } from './support/time';

interface Fixtures {
  /** Opt in per spec file for deterministic timers/Date.now (never in layout/resize specs — it captures rAF). */
  fakeClock: boolean;
  api: TelemetryApiMock;
  hub: SignalRHubMock;
  dashboard: DashboardPage;
}

export const test = base.extend<Fixtures>({
  fakeClock: [false, { option: true }],
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
});

export { expect } from '@playwright/test';
