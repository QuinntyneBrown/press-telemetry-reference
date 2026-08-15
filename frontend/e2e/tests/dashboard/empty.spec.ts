import { test, expect } from '../../fixtures';

test.describe('Empty state', () => {
  // Client rendering of a 200 [] snapshot (dashboard-empty.html).
  test('renders the No telemetry yet panel with the sample publish command', async ({ api, dashboard }) => {
    api.seedLatest([]);

    await dashboard.goto();

    await expect(dashboard.emptyPanel()).toBeVisible();
    await expect(dashboard.emptySnippet()).toBeVisible();
    await expect(dashboard.tiles()).toHaveCount(0);
  });
});
