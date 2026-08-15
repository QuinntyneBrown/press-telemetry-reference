import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';
import { freezeAt, isoAt } from '../../support/time';

test.use({ fakeClock: true });

test.describe('Stale snapshot points', () => {
  // L2-010 AC4 — a message older than the newest cached snapshot value never regresses the tile.
  test('an older point does not regress the snapshot tile', async ({ page, api, hub, dashboard }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.4 degC');
    await expect(dashboard.connection.indicator).toHaveText('Connected');
    await freezeAt(page, 30);

    hub.push({ deviceId: 'press-01', metric: 'temperature', value: 12.3, timestamp: isoAt(-100) });
    await page.clock.runFor(1100);

    await expect(dashboard.tile('press-01', 'temperature').value()).toHaveText('87.4 degC');
  });
});
