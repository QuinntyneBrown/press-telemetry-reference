import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';
import { freezeAt, isoAt } from '../../support/time';

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

  // L2-005 AC2 + L2-010 — a custom historical window is a fixed frame: live points
  // arriving while it is displayed belong to live-tracking windows (presets, overview
  // cards), never to an explicitly chosen past range, so the empty overlay persists.
  test('live points do not leak into a fixed historical range', async ({ page, api, hub, seriesDetail }) => {
    api.seedLatest(defaultDashboard()); // no range seed → every range query returns []

    await seriesDetail.goto('press-01', 'temperature');
    await expect(seriesDetail.emptyOverlay()).toBeVisible();

    const dialog = await seriesDetail.openRangeDialog();
    await dialog.fromInput().fill('2026-08-13T00:00');
    await dialog.toInput().fill('2026-08-13T06:00');
    await dialog.applyButton().click();
    await expect(seriesDetail.chip('Custom')).toHaveAttribute('aria-pressed', 'true');
    await expect(seriesDetail.emptyOverlay()).toBeVisible();
    await freezeAt(page, 30);

    hub.push({ deviceId: 'press-01', metric: 'temperature', value: 88.1, timestamp: isoAt(31) });
    await page.clock.runFor(1100);

    await expect(seriesDetail.emptyOverlay()).toBeVisible();
    await expect(seriesDetail.card().line()).toHaveCount(0);
  });
});
