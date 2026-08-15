import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

test.describe('Connection lifecycle across navigation', () => {
  // L2-012 AC4 — when the component owning the connection unmounts, the hub
  // connection is stopped: navigating between views never accumulates sockets.
  test('navigation never leaks hub connections', async ({ api, hub, dashboard, seriesDetail }) => {
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.connection.indicator).toHaveText('Connected');

    await dashboard.tile('press-01', 'temperature').root.click();
    await expect(seriesDetail.title('press-01', 'temperature')).toBeVisible();
    await expect(seriesDetail.connection.indicator).toHaveText('Connected');

    await seriesDetail.backLink().click();
    await expect(dashboard.title()).toBeVisible();
    await expect(dashboard.connection.indicator).toHaveText('Connected');

    await expect.poll(() => hub.openSockets).toBe(1);
  });
});
