import type { Page, WebSocketRoute } from '@playwright/test';
import type { TelemetryPoint } from './telemetry-api-mock';

const RS = '\x1e'; // SignalR record separator

/**
 * Mock of the /hubs/telemetry SignalR hub over the JSON hub protocol via
 * page.routeWebSocket. The app connects with skipNegotiation + WebSockets, so
 * one WS route covers the whole hub. Frames may arrive concatenated — always
 * split on \x1e. The handshake reply must be synchronous or start() hangs.
 */
export class SignalRHubMock {
  private current: WebSocketRoute | undefined;
  private refuse = false;
  /** Sockets opened (including refused) — L2-012 AC2 retry counting. */
  attempts = 0;
  /** Completed handshakes. */
  connections = 0;
  /** Currently open sockets — leak detection across navigations. */
  openSockets = 0;
  private waiters: { n: number; resolve: () => void }[] = [];

  async install(page: Page): Promise<void> {
    await page.routeWebSocket(/\/hubs\/telemetry/, ws => {
      this.attempts += 1;
      if (this.refuse) {
        ws.close({ code: 1011, reason: 'e2e: hub unavailable' });
        return;
      }
      this.openSockets += 1;
      this.current = ws;
      ws.onClose(() => {
        this.openSockets -= 1;
        if (this.current === ws) this.current = undefined;
      });
      ws.onMessage(raw => {
        for (const frame of String(raw).split(RS).filter(Boolean)) {
          const msg = JSON.parse(frame) as { protocol?: string; type?: number };
          if (msg.protocol === 'json') {
            ws.send('{}' + RS); // handshake success — must be synchronous
            this.connections += 1;
            this.waiters = this.waiters.filter(w => (this.connections >= w.n ? (w.resolve(), false) : true));
          } else if (msg.type === 6) {
            ws.send('{"type":6}' + RS); // pong
          }
        }
      });
    });
  }

  /** Broadcast one telemetry point to the connected client. */
  push(point: TelemetryPoint): void {
    if (!this.current) throw new Error('hub.push before any client connected');
    this.current.send(JSON.stringify({ type: 1, target: 'telemetry', arguments: [point] }) + RS);
  }

  /** Server-side drop; the client enters Reconnecting (L2-012). */
  drop(): void {
    if (!this.current) return;
    this.current.send(`{"type":7,"allowReconnect":true}${RS}`);
    this.current.close({ code: 1000, reason: 'e2e: server going away' });
    this.current = undefined;
  }

  refuseConnections(on: boolean): void {
    this.refuse = on;
  }

  /** Resolves once at least n handshakes have completed (pass 2 after a drop). */
  waitForConnection(n = 1): Promise<void> {
    if (this.connections >= n) return Promise.resolve();
    return new Promise(resolve => this.waiters.push({ n, resolve }));
  }
}
