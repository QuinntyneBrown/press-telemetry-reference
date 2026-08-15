import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { ConnectionState } from '@press/dashboard-core';
import { hubUrl } from './config';
import { telemetryKeys } from './queryKeys';
import { TelemetryHubClient } from './TelemetryHubClient';

/**
 * Owns the hub connection for the mounted view: connects on mount, surfaces
 * connection state, invalidates telemetry queries after a reconnect so missed
 * points backfill from REST (L2-012 AC1), stops the connection on unmount (AC4).
 */
export function useLiveTelemetry(): ConnectionState {
  const queryClient = useQueryClient();
  const [state, setState] = useState<ConnectionState>('Connecting');

  useEffect(() => {
    const client = new TelemetryHubClient(hubUrl, {
      onPoint: () => {},
      onStateChange: setState,
      onReconnected: () => queryClient.invalidateQueries({ queryKey: telemetryKeys.all }),
    });
    void client.connect();
    return () => {
      void client.stop();
    };
  }, [queryClient]);

  return state;
}
