import { test, expect } from '../../fixtures';
import { defaultDashboard, rangePoints } from '../../support/seed';

// Real clock — ResizeObserver/rAF must run naturally.

test.describe('Chart resize', () => {
  // L2-016 AC4 — a visible chart redraws to fit its container when the container resizes.
  test('chart redraws to its container on viewport resize', async ({ page, api, dashboard }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    api.seedLatest(defaultDashboard());
    const now = new Date();
    api.seedRange(
      'press-01',
      'temperature',
      rangePoints(new Date(now.getTime() - 6 * 60_000), now, 5, i => 80 + (i % 10)),
    );

    await dashboard.goto();
    const card = dashboard.chartCard('press-01', 'temperature');
    await expect(card.line()).toBeVisible();
    const narrowBox = (await card.chart().boundingBox())!;
    const labelsBefore = await card.xLabels().count();

    // 1280px lays the grid out in 3 columns; 700px collapses to 1 → the chart's
    // container (and so the chart) gets wider and the x-axis re-labels.
    await page.setViewportSize({ width: 700, height: 900 });

    await expect.poll(async () => (await card.chart().boundingBox())!.width).toBeGreaterThan(narrowBox.width);
    await expect.poll(() => card.xLabels().count()).toBeGreaterThanOrEqual(labelsBefore);
    const cardBox = (await card.root.boundingBox())!;
    const chartBox = (await card.chart().boundingBox())!;
    expect(chartBox.width).toBeLessThanOrEqual(cardBox.width);
  });
});
