import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';
import { freezeAt, isoAt } from '../../support/time';

test.use({ fakeClock: true });

test.describe('No refetch during live updates', () => {
  // L2-009 AC3 — 60 seconds of live updates (time-compressed) cause zero telemetry REST
  // requests; the cache is patched in place.
  test('sixty seconds of hub traffic triggers no REST requests', async ({ page, api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.4 degC');
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    const latestCalls = api.calls.latest;
    const rangeCalls = api.calls.range.length;
    await freezeAt(page, 30);

    for (let s = 1; s <= 60; s++) {
      hub.push({ deviceId: 'press-01', metric: 'temperature', value: 87 + s / 100, timestamp: isoAt(30 + s) });
      await page.clock.runFor(1000);
    }

    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.6 degC');
    expect(api.calls.latest).toBe(latestCalls);
    expect(api.calls.range.length).toBe(rangeCalls);
  });
});
