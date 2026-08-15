import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

test.describe('Error state', () => {
  // L2-009 AC2 — a failed query shows an error state with a retry action, not a broken layout.
  test('shows the error panel; Retry refetches and recovers', async ({ api, dashboard }) => {
    api.seedLatest(defaultDashboard()).failLatest();

    await dashboard.goto();

    await expect(dashboard.errorPanel()).toBeVisible();
    await expect(dashboard.retryButton()).toBeVisible();
    await expect(dashboard.tiles()).toHaveCount(0);

    api.restoreLatest();
    await dashboard.retryButton().click();

    await expect(dashboard.tiles()).toHaveCount(8);
    await expect(dashboard.errorPanel()).toHaveCount(0);
    expect(api.calls.latest).toBe(2);
  });
});
