import { test, expect } from '../../fixtures';
import { defaultDashboard, rangePoints } from '../../support/seed';
import { T0 } from '../../support/time';

test.use({ fakeClock: true });

const DAY_MS = 24 * 60 * 60 * 1000;

test.describe('Series detail navigation', () => {
  // L2-009 AC1 on the detail route + L2-005 client — a tile links to its series view,
  // which hydrates a 24-hour default window from the range endpoint.
  test('tile navigates to series detail with a hydrated 24H default window', async ({
    page,
    api,
    dashboard,
    seriesDetail,
  }) => {
    api.seedLatest(defaultDashboard());
    // Seed a bit over 24h back so drift never empties the window; 10-minute cadence.
    const seedFrom = new Date(T0.getTime() - DAY_MS - 60 * 60_000);
    api.seedRange('press-01', 'temperature', rangePoints(seedFrom, T0, 600, i => 60 + (i % 30)));

    await dashboard.goto();
    await expect(dashboard.tiles()).toHaveCount(8);
    await dashboard.tile('press-01', 'temperature').root.click();

    await expect(page).toHaveURL(/\/series\/press-01\/temperature$/);
    await expect(seriesDetail.title('press-01', 'temperature')).toBeVisible();
    await expect(seriesDetail.backLink()).toBeVisible();
    await expect(seriesDetail.chip('24H')).toHaveAttribute('aria-pressed', 'true');
    await expect(seriesDetail.chip('1H')).toHaveAttribute('aria-pressed', 'false');

    await expect(seriesDetail.card().line()).toBeVisible();
    expect(await seriesDetail.card().pointCount()).toBeGreaterThan(100);
    await expect(seriesDetail.currentValue()).toHaveText('current 87.4 degC');
    await expect(seriesDetail.card().liveTag()).toBeVisible();
    await expect(seriesDetail.rangeLabel()).toHaveText(
      /^\d{4}-\d{2}-\d{2} \d{2}:\d{2} → \d{4}-\d{2}-\d{2} \d{2}:\d{2} UTC$/,
    );

    // The detail hydration requested exactly a 24-hour window for this series.
    const call = api.calls.range.findLast(u => u.pathname === '/api/telemetry/press-01/temperature');
    expect(call).toBeTruthy();
    const from = Date.parse(call!.searchParams.get('from')!);
    const to = Date.parse(call!.searchParams.get('to')!);
    expect(to - from).toBe(DAY_MS);

    await seriesDetail.backLink().click();
    await expect(dashboard.title()).toBeVisible();
  });
});
