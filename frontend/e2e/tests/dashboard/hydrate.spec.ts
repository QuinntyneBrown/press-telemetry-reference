import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

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
