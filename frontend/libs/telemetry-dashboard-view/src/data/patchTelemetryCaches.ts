import type { QueryClient } from '@tanstack/react-query';
import type { SeriesPoint } from '@press/dashboard-core';
import { telemetryKeys } from './queryKeys';
import { seriesKeyOf, type TelemetryPoint } from './types';

/**
 * Pure cache update (L2-010): the snapshot advances only for newer timestamps
 * (latest-wins per series, never regresses — AC4); cached range data receives
 * in-order appends, and points not strictly newer than the newest cached range
 * point are discarded (AC2/AC3 — the reconnect refetch heals any gap).
 * Timestamps compare via Date.parse, never lexicographically.
 */
export function patchTelemetryCaches(queryClient: QueryClient, points: TelemetryPoint[]): void {
  if (points.length === 0) return;

  queryClient.setQueryData<TelemetryPoint[]>(telemetryKeys.latest, (current = []) => {
    const bySeries = new Map(current.map(p => [seriesKeyOf(p), p]));
    for (const p of points) {
      const key = seriesKeyOf(p);
      const existing = bySeries.get(key);
      if (!existing || Date.parse(p.timestamp) > Date.parse(existing.timestamp)) {
        bySeries.set(key, p);
      }
    }
    return [...bySeries.values()];
  });

  for (const [queryKey, data] of queryClient.getQueriesData<SeriesPoint[]>({
    queryKey: ['telemetry', 'range'],
  })) {
    if (!data) continue;
    const [, , deviceId, metric] = queryKey as readonly string[];
    let newest = data.length ? Date.parse(data[data.length - 1].timestamp) : -Infinity;
    const additions: SeriesPoint[] = [];
    const seriesPoints = points
      .filter(p => p.deviceId === deviceId && p.metric === metric)
      .sort((a, b) => Date.parse(a.timestamp) - Date.parse(b.timestamp));
    for (const p of seriesPoints) {
      const t = Date.parse(p.timestamp);
      if (t > newest) {
        additions.push({ timestamp: p.timestamp, value: p.value });
        newest = t;
      }
    }
    if (additions.length) queryClient.setQueryData(queryKey, [...data, ...additions]);
  }
}
