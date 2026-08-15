import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';
import { freezeAt, isoAt } from '../../support/time';

const RETRY_DELAY_MS = 2000; // mirrors data/config.ts

test.use({ fakeClock: true });

test.describe('Reconnect lifecycle', () => {
  // L2-012 AC3 — a visible indicator shows the reconnecting state; live affordances pause.
  test('drop shows Reconnecting, removes pulses, swaps Live tags for paused', async ({
    page,
    api,
    hub,
    dashboard,
  }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    await freezeAt(page, 30);
    hub.push({ deviceId: 'press-01', metric: 'temperature', value: 88.1, timestamp: isoAt(31) });
    await page.clock.runFor(1100);
    const tile = dashboard.tile('press-01', 'temperature');
    await expect(tile.pulse()).toBeVisible();
    const card = dashboard.chartCard('press-01', 'temperature');
    await expect(card.liveTag()).toBeVisible();

    hub.drop();

    await expect(dashboard.connection.indicator).toHaveText('Reconnecting');
    await expect(dashboard.pausedBanner()).toBeVisible();
    await expect(tile.pulse()).toHaveCount(0);
    await expect(card.pausedTag()).toBeVisible();
    await expect(card.liveTag()).toHaveCount(0);
  });

  // L2-012 AC1 — reconnection invalidates telemetry queries; values missed during
  // the outage appear after the refetch.
  test('reconnect refetches and backfilled values appear', async ({ page, api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    const latestCalls = api.calls.latest;
    await freezeAt(page, 30);

    hub.drop();
    await expect(dashboard.connection.indicator).toHaveText('Reconnecting');

    // Points "missed during the outage" land in the snapshot seed.
    const missed = defaultDashboard();
    missed[0] = { ...missed[0], value: 91.5, timestamp: isoAt(35) };
    api.seedLatest(missed);

    await page.clock.runFor(RETRY_DELAY_MS + 100);
    await hub.waitForConnection(2);
    // Resume auto-tick: the post-reconnect refetch renders through timer-scheduled
    // cache notifications, which stay queued under a frozen clock.
    await page.clock.resume();

    await expect(dashboard.connection.indicator).toHaveText('Connected');
    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('91.5 degC');
    expect(api.calls.latest).toBe(latestCalls + 1);
  });

  // L2-012 AC2 — retries never stop (the default policy would stop after four
  // attempts); the client reconnects without a page reload after a long outage.
  test('keeps retrying beyond four attempts and recovers', async ({ page, api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    await freezeAt(page, 30);

    hub.refuseConnections(true);
    const attemptsBefore = hub.attempts;
    hub.drop();
    await expect(dashboard.connection.indicator).toHaveText('Reconnecting');

    // > 60 compressed seconds of outage.
    for (let i = 0; i < 35; i++) {
      await page.clock.runFor(RETRY_DELAY_MS);
    }
    expect(hub.attempts - attemptsBefore).toBeGreaterThanOrEqual(5);

    hub.refuseConnections(false);
    await page.clock.runFor(RETRY_DELAY_MS + 100);
    await hub.waitForConnection(2);
    await expect(dashboard.connection.indicator).toHaveText('Connected');
  });
});
