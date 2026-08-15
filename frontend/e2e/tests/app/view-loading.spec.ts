import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

test.describe('Lazy view loading', () => {
  // L2-014 AC3 — the first navigation fetches the view library on demand; a
  // Suspense fallback shows until the module arrives. Behaviour only — chunk
  // filenames are never asserted.
  test('first navigation shows the Loading view fallback, then renders the view', async ({
    page,
    api,
    dashboard,
  }) => {
    api.seedLatest(defaultDashboard());
    let release!: () => void;
    const gate = new Promise<void>(resolve => {
      release = resolve;
    });
    let viewRequests = 0;
    await page.route(/telemetry-dashboard-view\/src\/index\.ts/, async route => {
      viewRequests += 1;
      await gate;
      await route.continue();
    });

    await page.goto('/');

    const spinner = page.getByRole('status', { name: 'Loading view' });
    await expect(spinner).toBeVisible();
    await expect(page.getByText('Loading view…')).toBeVisible();
    await expect(page.getByText('telemetry-dashboard-view', { exact: true })).toBeVisible();

    release();

    await expect(dashboard.tiles()).toHaveCount(8);
    await expect(spinner).toHaveCount(0);
    expect(viewRequests).toBeGreaterThanOrEqual(1);
  });
});
