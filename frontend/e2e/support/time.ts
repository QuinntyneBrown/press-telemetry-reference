/** Fake-clock "now" for deterministic specs. Seed timestamps sit a few seconds earlier. */
export const T0 = new Date('2026-08-15T12:00:10Z');

/** ISO-8601 UTC without milliseconds, offset in seconds from T0 (matches mock formatting). */
export const isoAt = (secondsFromT0: number): string =>
  new Date(T0.getTime() + secondsFromT0 * 1000).toISOString().replace(/\.\d{3}Z$/, 'Z');

/**
 * Fast-forward the fake clock to T0 + seconds and pause there. Call only AFTER
 * hydration (a paused clock during page load blanks the app); pending timers up
 * to that instant fire on the way.
 */
export const freezeAt = async (page: import('@playwright/test').Page, secondsFromT0: number): Promise<Date> => {
  const at = new Date(T0.getTime() + secondsFromT0 * 1000);
  await page.clock.pauseAt(at);
  return at;
};
