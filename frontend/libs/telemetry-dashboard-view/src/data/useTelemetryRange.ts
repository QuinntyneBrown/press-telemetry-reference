import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import type { SeriesPoint } from '@press/dashboard-core';
import { apiBaseUrl } from './config';
import { telemetryKeys } from './queryKeys';
import type { SeriesKey, TimeRange } from './types';

async function fetchRange(series: SeriesKey, fromIso: string, toIso: string): Promise<SeriesPoint[]> {
  const url =
    `${apiBaseUrl}/api/telemetry/${series.deviceId}/${series.metric}` +
    `?from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}`;
  const response = await fetch(url);
  if (!response.ok) throw new Error(`GET range for ${series.deviceId}/${series.metric} failed with ${response.status}`);
  return response.json();
}

/** Loads the visible chart window [from, to) on demand (L2-005 client side). */
export function useTelemetryRange(series: SeriesKey, range: TimeRange): UseQueryResult<SeriesPoint[]> {
  const fromIso = range.from.toISOString();
  const toIso = range.to.toISOString();
  return useQuery({
    queryKey: telemetryKeys.range(series.deviceId, series.metric, fromIso, toIso),
    queryFn: () => fetchRange(series, fromIso, toIso),
  });
}
