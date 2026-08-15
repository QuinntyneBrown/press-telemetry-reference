import type { TelemetryPoint, TimeRange } from './data/types';

const dayMinute = (d: Date): string => `${d.toISOString().slice(0, 10)} ${d.toISOString().slice(11, 16)}`;

/** "YYYY-MM-DD HH:MM → YYYY-MM-DD HH:MM UTC" (series-detail toolbar). */
export function formatRangeLabel(range: TimeRange): string {
  return `${dayMinute(range.from)} → ${dayMinute(range.to)} UTC`;
}

/** "HH:MMZ" of the newest snapshot timestamp — the "values as of" label while paused. */
export function asOfTimeLabel(points: TelemetryPoint[]): string {
  const newest = Math.max(...points.map(p => Date.parse(p.timestamp)));
  return `${new Date(newest).toISOString().slice(11, 16)}Z`;
}
