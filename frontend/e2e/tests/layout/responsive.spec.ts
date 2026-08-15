import { test, expect } from '../../fixtures';
import { defaultDashboard } from '../../support/seed';

// Real clock throughout — the fake clock captures rAF and stalls ResizeObserver-driven work.

test.describe('Responsive layout', () => {
  // L2-016 AC1 — 320px: tiles and charts stack in a single column, no horizontal scroll.
  test('320px: single column with no horizontal scroll', async ({ page, api, dashboard }) => {
    await page.setViewportSize({ width: 320, height: 800 });
    api.seedLatest(defaultDashboard());

    await dashboard.goto();
    await expect(dashboard.tiles()).toHaveCount(8);

    expect(await dashboard.columnCount()).toBe(1);
    expect(await dashboard.hasHorizontalScroll()).toBe(false);
  });

  // L2-016 AC2 — at least two columns at 768px, at least three at 1200px.
  test('768px shows at least two columns; 1200px at least three', async ({ page, api, dashboard }) => {
    api.seedLatest(defaultDashboard());
    await page.setViewportSize({ width: 768, height: 800 });

    await dashboard.goto();
    await expect(dashboard.tiles()).toHaveCount(8);
    expect(await dashboard.columnCount()).toBeGreaterThanOrEqual(2);

    await page.setViewportSize({ width: 1200, height: 800 });
    await expect.poll(() => dashboard.columnCount()).toBeGreaterThanOrEqual(3);
  });

  // L2-016 AC3 (spot-check) — no clipping/overlap across the sweep, measured as
  // the absence of horizontal page scroll.
  test('no horizontal scroll from 320px to 1920px', async ({ page, api, dashboard }) => {
    api.seedLatest(defaultDashboard());
    await dashboard.goto();
    await expect(dashboard.tiles()).toHaveCount(8);

    for (const width of [320, 576, 768, 992, 1200, 1600, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      expect(await dashboard.hasHorizontalScroll(), `no horizontal scroll at ${width}px`).toBe(false);
    }
  });
});
