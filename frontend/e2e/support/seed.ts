import type { TelemetryPoint, SeriesPoint } from './telemetry-api-mock';
import { T0 } from './time';

/** Canonical snapshot mirroring docs/mocks/dashboard.html exactly (values, order, timestamps). */
export function defaultDashboard(): TelemetryPoint[] {
  return [
    { deviceId: 'press-01', metric: 'temperature', value: 87.4, timestamp: '2026-08-15T12:00:07Z' },
    { deviceId: 'press-01', metric: 'pressure', value: 210.3, timestamp: '2026-08-15T12:00:07Z' },
    { deviceId: 'press-01', metric: 'vibration', value: 2.8, timestamp: '2026-08-15T12:00:06Z' },
    { deviceId: 'press-01', metric: 'cycle_count', value: 148392, timestamp: '2026-08-15T12:00:07Z' },
    { deviceId: 'press-02', metric: 'temperature', value: 79.1, timestamp: '2026-08-15T12:00:05Z' },
    { deviceId: 'press-02', metric: 'pressure', value: 198.7, timestamp: '2026-08-15T12:00:05Z' },
    { deviceId: 'press-02', metric: 'vibration', value: 4.6, timestamp: '2026-08-15T12:00:04Z' },
    { deviceId: 'press-02', metric: 'cycle_count', value: 131077, timestamp: '2026-08-15T12:00:05Z' },
  ];
}

/** Evenly spaced points covering the closed-start window [from, to) at `stepSeconds`. */
export function rangePoints(from: Date, to: Date, stepSeconds: number, value: (i: number) => number): SeriesPoint[] {
  const points: SeriesPoint[] = [];
  for (let t = from.getTime(), i = 0; t < to.getTime(); t += stepSeconds * 1000, i++) {
    points.push({
      timestamp: new Date(t).toISOString().replace(/\.\d{3}Z$/, 'Z'),
      value: value(i),
    });
  }
  return points;
}

/** Last-5-minutes window ending at T0 — the overview chart hydration window. */
export function lastFiveMinutes(): { from: Date; to: Date } {
  return { from: new Date(T0.getTime() - 5 * 60_000), to: T0 };
}
