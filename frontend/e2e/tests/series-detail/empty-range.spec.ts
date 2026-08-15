import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

test.use({ fakeClock: true });

test.describe('Empty range', () => {
  // L2-005 AC2 client rendering — an empty result keeps the axis frame and overlays
  // "No data in this range" with the range echoed.
  test('empty range keeps the axis frame and shows the empty overlay', async ({ api, seriesDetail }) => {
    api.seedLatest(defaultDashboard()); // no range seed → 200 []

    await seriesDetail.goto('press-01', 'temperature');

    await expect(seriesDetail.emptyOverlay()).toBeVisible();
    await expect(seriesDetail.card().chart()).toBeVisible();
    await expect(seriesDetail.card().line()).toHaveCount(0);
    await expect(seriesDetail.card().liveTag()).toHaveCount(0);
    await expect(seriesDetail.currentValue()).toHaveCount(0);
  });
});
