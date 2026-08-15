import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { apiBaseUrl } from './config';
import { telemetryKeys } from './queryKeys';
import type { TelemetryPoint } from './types';

async function fetchLatest(): Promise<TelemetryPoint[]> {
  const response = await fetch(`${apiBaseUrl}/api/telemetry/latest`);
  if (!response.ok) throw new Error(`GET /api/telemetry/latest failed with ${response.status}`);
  return response.json();
}

/** Loads the newest point per known series — the dashboard's single first-render call (L2-009). */
export function useTelemetrySnapshot(): UseQueryResult<TelemetryPoint[]> {
  return useQuery({ queryKey: telemetryKeys.latest, queryFn: fetchLatest });
}
