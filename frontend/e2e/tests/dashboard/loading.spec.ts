import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

test.describe('Loading state', () => {
  // L2-009 AC2 — a loading state is shown while queries are in flight.
  test('shows aria-busy skeletons while the snapshot is in flight, then tiles', async ({ api, dashboard }) => {
    api.seedLatest(defaultDashboard()).holdLatest();

    await dashboard.goto();

    await expect(dashboard.loadingGrid()).toBeVisible();
    await expect(dashboard.tiles()).toHaveCount(0);

    api.releaseLatest();

    await expect(dashboard.tiles()).toHaveCount(8);
    await expect(dashboard.loadingGrid()).toHaveCount(0);
  });
});
