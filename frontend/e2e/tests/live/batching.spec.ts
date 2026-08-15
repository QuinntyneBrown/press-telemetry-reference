import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';
import { freezeAt, isoAt } from '../../support/time';

test.use({ fakeClock: true });

test.describe('~1 Hz batching', () => {
  // L2-011 AC1/AC3 — many messages within one flush window commit once; latest value
  // per series wins for the snapshot. Nothing renders before the flush boundary.
  test('buffers pushes until the flush; latest per series wins', async ({ page, api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.4 degC');
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    await freezeAt(page, 30);

    for (let i = 0; i < 5; i++) {
      hub.push({ deviceId: 'press-01', metric: 'temperature', value: 90 + i, timestamp: isoAt(31 + i) });
    }
    hub.push({ deviceId: 'press-02', metric: 'pressure', value: 200.5, timestamp: isoAt(31) });

    // Clock frozen → no flush yet: the tiles still show hydrated values.
    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.4 degC');
    await expect(dashboard.tile('press-02', 'pressure').value()).toHaveText('198.7 bar');

    await page.clock.runFor(1100);

    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('94 degC');
    await expect(dashboard.tile('press-02', 'pressure').value()).toHaveText('200.5 bar');
    expect(api.calls.latest).toBe(1);
  });
});
