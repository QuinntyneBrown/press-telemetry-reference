import { test, expect } from '../../fixtures';
import { defaultDashboard, rangePoints } from '../../support/seed';
import { T0 } from '../../support/time';

test.use({ fakeClock: true });

test.describe('Time-conductor chips', () => {
  // L2-005 client — preset chips request matching [from, to) windows.
  test('1H and 6H chips refetch matching windows and mark the active chip', async ({
    api,
    seriesDetail,
  }) => {
    api.seedLatest(defaultDashboard());
    const seedFrom = new Date(T0.getTime() - 25 * 60 * 60_000);
    api.seedRange('press-01', 'temperature', rangePoints(seedFrom, T0, 600, i => 60 + (i % 30)));

    await seriesDetail.goto('press-01', 'temperature');
    await expect(seriesDetail.chip('24H')).toHaveAttribute('aria-pressed', 'true');

    await seriesDetail.chip('1H').click();
    await expect(seriesDetail.chip('1H')).toHaveAttribute('aria-pressed', 'true');
    await expect(seriesDetail.chip('24H')).toHaveAttribute('aria-pressed', 'false');
    await expect.poll(() => {
      const last = api.calls.range.at(-1)!;
      return Date.parse(last.searchParams.get('to')!) - Date.parse(last.searchParams.get('from')!);
    }).toBe(60 * 60_000);

    await seriesDetail.chip('6H').click();
    await expect(seriesDetail.chip('6H')).toHaveAttribute('aria-pressed', 'true');
    await expect(seriesDetail.chip('1H')).toHaveAttribute('aria-pressed', 'false');
    await expect.poll(() => {
      const last = api.calls.range.at(-1)!;
      return Date.parse(last.searchParams.get('to')!) - Date.parse(last.searchParams.get('from')!);
    }).toBe(6 * 60 * 60_000);
  });
});
