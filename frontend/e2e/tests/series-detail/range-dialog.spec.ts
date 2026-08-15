import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

test.describe('Custom range dialog', () => {
  // L2-005 client — a valid custom range applies, closes the dialog, and refetches [from, to).
  test('Custom… opens a modal dialog; a valid range applies and refetches', async ({ api, seriesDetail }) => {
    api.seedLatest(defaultDashboard());
    await seriesDetail.goto('press-01', 'temperature');

    const dialog = await seriesDetail.openRangeDialog();
    await expect(dialog.root).toHaveAttribute('aria-modal', 'true');
    await expect(dialog.hint()).toBeVisible();

    await dialog.fromInput().fill('2026-08-13T00:00');
    await dialog.toInput().fill('2026-08-13T06:00');
    const callsBefore = api.calls.range.length;
    await dialog.applyButton().click();

    await expect(dialog.root).toHaveCount(0);
    await expect(seriesDetail.chip('Custom')).toHaveAttribute('aria-pressed', 'true');
    await expect(seriesDetail.rangeLabel()).toHaveText('2026-08-13 00:00 → 2026-08-13 06:00 UTC');
    await expect.poll(() => api.calls.range.length).toBeGreaterThan(callsBefore);
    const last = api.calls.range.at(-1)!;
    expect(Date.parse(last.searchParams.get('from')!)).toBe(Date.parse('2026-08-13T00:00:00Z'));
    expect(Date.parse(last.searchParams.get('to')!)).toBe(Date.parse('2026-08-13T06:00:00Z'));
  });

  // L2-005 AC5 client-side guard — a window over 24 hours never reaches the API.
  test('a range wider than 24 hours shows the span error and disables Apply', async ({ api, seriesDetail }) => {
    api.seedLatest(defaultDashboard());
    await seriesDetail.goto('press-01', 'temperature');
    // Initial 24H hydration has settled once the (empty) plot overlay renders.
    await expect(seriesDetail.emptyOverlay()).toBeVisible();
    const callsBefore = api.calls.range.length;

    const dialog = await seriesDetail.openRangeDialog();
    await dialog.fromInput().fill('2026-08-14T04:00');
    await dialog.toInput().fill('2026-08-15T10:00');

    await expect(dialog.error()).toHaveText('Range spans 30 hours — maximum window is 24 hours.');
    await expect(dialog.fromInput()).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.toInput()).toHaveAttribute('aria-invalid', 'true');
    await expect(dialog.applyButton()).toBeDisabled();
    expect(api.calls.range.length).toBe(callsBefore);
  });

  // L2-005 AC4 client-side guard — inverted ranges are blocked; Cancel leaves the view untouched.
  test('from >= to shows the ordering error; Cancel closes without applying', async ({ api, seriesDetail }) => {
    api.seedLatest(defaultDashboard());
    await seriesDetail.goto('press-01', 'temperature');

    const dialog = await seriesDetail.openRangeDialog();
    await dialog.fromInput().fill('2026-08-15T12:00');
    await dialog.toInput().fill('2026-08-14T12:00');

    await expect(dialog.error()).toHaveText("'From' must be earlier than 'To'.");
    await expect(dialog.applyButton()).toBeDisabled();

    await dialog.cancelButton().click();
    await expect(dialog.root).toHaveCount(0);
    await expect(seriesDetail.chip('24H')).toHaveAttribute('aria-pressed', 'true');
  });
});
