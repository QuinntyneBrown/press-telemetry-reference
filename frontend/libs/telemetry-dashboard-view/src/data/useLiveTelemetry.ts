import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { ConnectionState } from '@press/dashboard-core';
import { FLUSH_INTERVAL_MS, hubUrl } from './config';
import { markSeriesArrived } from './liveness';
import { patchTelemetryCaches } from './patchTelemetryCaches';
import { telemetryKeys } from './queryKeys';
import { TelemetryBatcher } from './TelemetryBatcher';
import { TelemetryHubClient } from './TelemetryHubClient';

/**
 * Owns the hub connection for the mounted view: connects on mount, feeds the
 * ~1 Hz batcher into the query cache (L2-010/L2-011), surfaces connection
 * state, invalidates telemetry queries after a reconnect so missed points
 * backfill from REST (L2-012 AC1), stops everything on unmount (AC4).
 */
export function useLiveTelemetry(): ConnectionState {
  const queryClient = useQueryClient();
  const [state, setState] = useState<ConnectionState>('Connecting');
  const [, setTick] = useState(0);

  useEffect(() => {
    const batcher = new TelemetryBatcher(FLUSH_INTERVAL_MS, points => {
      if (points.length > 0) {
        markSeriesArrived(points);
        patchTelemetryCaches(queryClient, points);
      }
      setTick(t => t + 1); // 1 Hz re-render lets pulses expire on an idle stream
    });
    const client = new TelemetryHubClient(hubUrl, {
      onPoint: point => batcher.enqueue(point),
      onStateChange: setState,
      onReconnected: () => queryClient.invalidateQueries({ queryKey: telemetryKeys.all }),
    });
    void client.connect();
    return () => {
      batcher.dispose();
      void client.stop();
    };
  }, [queryClient]);

  return state;
}
