import { test, expect } from '../../fixtures';
import { defaultDashboard, lastFiveMinutes, rangePoints } from '../../support/seed';

test.describe('App shell', () => {
  test('renders the brand and a connection indicator in the header', async ({ api, hub, dashboard }) => {
    api.seedLatest([]);
    hub.refuseConnections(true);

    await dashboard.goto();

    await expect(dashboard.brand()).toBeVisible();
    await expect(dashboard.connection.indicator).toHaveText('Connecting');
  });

  // L2-012 AC3 — connection state is visible in the UI; reaches Connected after the hub handshake.
  test('indicator transitions to Connected once the hub handshake completes', async ({ api, dashboard }) => {
    api.seedLatest([]);

    await dashboard.goto();

    await expect(dashboard.connection.indicator).toHaveText('Connected');
  });
});

test.describe('Snapshot hydration', () => {
  // L2-009 AC1 — current values render from REST responses even if no SignalR message ever arrives.
  test('renders one tile per series from the snapshot endpoint', async ({ api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());
    hub.refuseConnections(true); // REST alone must render — no hub traffic at all

    await dashboard.goto();

    await expect(dashboard.tiles()).toHaveCount(8);
    const tile = dashboard.tile('press-01', 'temperature');
    await expect(tile.root.getByText('temperature')).toBeVisible();
    await expect(tile.root.getByText('press-01')).toBeVisible();
    await expect(tile.value()).toHaveText('87.4 degC');
    await expect(tile.timestamp()).toHaveText('2026-08-15T12:00:07Z');
    await expect(dashboard.tile('press-02', 'cycle_count').value()).toHaveText('131077 cycles');
  });

  test('toolbar shows the Overview title and device/series counts', async ({ api, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();

    await expect(dashboard.title()).toBeVisible();
    await expect(dashboard.summary()).toHaveText('2 devices · 8 series');
  });
});

test.describe('Overview charts', () => {
  test.use({ fakeClock: true });

  // L2-009 AC1 — the range endpoint hydrates visible charts through TanStack Query.
  test('renders chart cards for the first two snapshot series from seeded range data', async ({ api, dashboard }) => {
    api.seedLatest(defaultDashboard());
    // Seed a minute wider than the window so auto-tick drift never empties it.
    const { to } = lastFiveMinutes();
    const seedFrom = new Date(to.getTime() - 6 * 60_000);
    api.seedRange('press-01', 'temperature', rangePoints(seedFrom, to, 5, i => 87 + (i % 10) / 10));
    api.seedRange('press-01', 'pressure', rangePoints(seedFrom, to, 5, i => 206 + (i % 8)));

    await dashboard.goto();

    const temperature = dashboard.chartCard('press-01', 'temperature');
    await expect(temperature.chart()).toBeVisible();
    await expect(temperature.line()).toBeVisible();
    expect(await temperature.pointCount()).toBeGreaterThan(50);

    const pressure = dashboard.chartCard('press-01', 'pressure');
    await expect(pressure.chart()).toBeVisible();
    await expect(pressure.liveTag()).toBeVisible();

    // The requested window is exactly 5 minutes wide for both charts.
    const temperatureCall = api.calls.range.find(u => u.pathname === '/api/telemetry/press-01/temperature');
    const pressureCall = api.calls.range.find(u => u.pathname === '/api/telemetry/press-01/pressure');
    expect(temperatureCall).toBeTruthy();
    expect(pressureCall).toBeTruthy();
    for (const call of [temperatureCall!, pressureCall!]) {
      const from = Date.parse(call.searchParams.get('from')!);
      const to = Date.parse(call.searchParams.get('to')!);
      expect(to - from).toBe(5 * 60_000);
    }
  });
});
