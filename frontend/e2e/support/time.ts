/** Fake-clock "now" for deterministic specs. Seed timestamps sit a few seconds earlier. */
export const T0 = new Date('2026-08-15T12:00:10Z');

/** ISO-8601 UTC without milliseconds, offset in seconds from T0 (matches mock formatting). */
export const isoAt = (secondsFromT0: number): string =>
  new Date(T0.getTime() + secondsFromT0 * 1000).toISOString().replace(/\.\d{3}Z$/, 'Z');
