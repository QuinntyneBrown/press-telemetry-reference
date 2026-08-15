import { LIVE_WINDOW_MS } from './config';
import { seriesKeyOf, type SeriesKey, type TelemetryPoint } from './types';

// Arrival-time (not payload-timestamp) based: a tile pulses only while hub
// points for its series keep arriving (matches the mocks, immune to clock skew).
const lastHubArrival = new Map<string, number>();

export function markSeriesArrived(points: TelemetryPoint[]): void {
  const now = Date.now();
  for (const p of points) lastHubArrival.set(seriesKeyOf(p), now);
}

export function isSeriesLive(series: SeriesKey): boolean {
  const arrivedAt = lastHubArrival.get(seriesKeyOf(series));
  return arrivedAt !== undefined && Date.now() - arrivedAt <= LIVE_WINDOW_MS;
}
