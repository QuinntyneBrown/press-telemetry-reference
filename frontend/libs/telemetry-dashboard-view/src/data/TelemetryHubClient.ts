import { HubConnectionBuilder, HttpTransportType, type HubConnection } from '@microsoft/signalr';
import type { ConnectionState } from '@press/dashboard-core';
import { RETRY_DELAY_MS } from './config';
import type { TelemetryPoint } from './types';

export interface TelemetryHubHandlers {
  onPoint: (point: TelemetryPoint) => void;
  onStateChange: (state: ConnectionState) => void;
  onReconnected: () => void;
}

const delay = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

/**
 * Wrapper around @microsoft/signalr with an indefinite reconnect policy (L2-012):
 * the library default stops after four attempts, so the retry delay callback
 * always returns a delay. skipNegotiation + WebSockets is deliberate — one fewer
 * round-trip, at the cost of no SSE/long-polling fallback.
 */
export class TelemetryHubClient {
  private readonly connection: HubConnection;
  private stopped = false;

  constructor(hubUrl: string, handlers: TelemetryHubHandlers) {
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { skipNegotiation: true, transport: HttpTransportType.WebSockets })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => RETRY_DELAY_MS })
      .build();
    this.connection.on('telemetry', handlers.onPoint);
    this.connection.onreconnecting(() => handlers.onStateChange('Reconnecting'));
    this.connection.onreconnected(() => {
      handlers.onStateChange('Connected');
      handlers.onReconnected();
    });
    this.handlers = handlers;
  }

  private readonly handlers: TelemetryHubHandlers;

  /** withAutomaticReconnect covers drops, not a failed first start — retry that here too. */
  async connect(): Promise<void> {
    this.handlers.onStateChange('Connecting');
    while (!this.stopped) {
      try {
        await this.connection.start();
        this.handlers.onStateChange('Connected');
        return;
      } catch {
        await delay(RETRY_DELAY_MS);
      }
    }
  }

  async stop(): Promise<void> {
    this.stopped = true;
    await this.connection.stop();
  }
}
