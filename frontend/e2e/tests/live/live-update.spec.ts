import { test, expect } from '../../fixtures';
import { defaultDashboard, lastFiveMinutes, rangePoints } from '../../support/seed';
import { freezeAt, isoAt } from '../../support/time';

test.use({ fakeClock: true });

test.describe('Live tile updates', () => {
  // L2-010 AC1 — a `telemetry` message updates the tile and chart without any new REST request.
  // L2-011 AC2 — a single message on an idle stream is visible within 1 second.
  test('hub push updates the tile with no refetch', async ({ page, api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.4 degC');
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    const latestCalls = api.calls.latest;
    const rangeCalls = api.calls.range.length;
    await freezeAt(page, 30);

    hub.push({ deviceId: 'press-01', metric: 'temperature', value: 88.1, timestamp: isoAt(31) });
    await page.clock.runFor(1100);

    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('88.1 degC');
    await expect(dashboard.tile('press-01', 'temperature').pulse()).toBeVisible();
    expect(api.calls.latest).toBe(latestCalls);
    expect(api.calls.range.length).toBe(rangeCalls);
  });

  // L2-010 AC2/AC3 — in-order live points append to cached range data; stale points are discarded.
  test('live points append to the cached range in order; stale points are discarded', async ({
    page,
    api,
    hub,
    dashboard,
  }) => {
    api.seedLatest(defaultDashboard());
    const { to } = lastFiveMinutes();
    const seedFrom = new Date(to.getTime() - 6 * 60_000);
    api.seedRange('press-01', 'temperature', rangePoints(seedFrom, to, 5, i => 87 + (i % 10) / 10));

    await dashboard.goto();
    const card = dashboard.chartCard('press-01', 'temperature');
    await expect(card.line()).toBeVisible();
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    const before = await card.pointCount();
    await freezeAt(page, 30);

    hub.push({ deviceId: 'press-01', metric: 'temperature', value: 88.2, timestamp: isoAt(31) });
    await page.clock.runFor(1100);
    expect(await card.pointCount()).toBe(before + 1);

    // Older than the newest cached range point → discarded (reconnect refetch heals gaps).
    hub.push({ deviceId: 'press-01', metric: 'temperature', value: 50.0, timestamp: isoAt(-120) });
    await page.clock.runFor(1100);
    expect(await card.pointCount()).toBe(before + 1);
  });
});
