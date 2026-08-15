import type { TelemetryPoint } from './types';

/**
 * Buffers inbound hub points and flushes them at ~1 Hz (L2-011). The interval
 * always ticks — empty flushes double as the cadence for pulse-expiry re-checks.
 */
export class TelemetryBatcher {
  private pending: TelemetryPoint[] = [];
  private readonly timer: ReturnType<typeof setInterval>;

  constructor(intervalMs: number, onFlush: (points: TelemetryPoint[]) => void) {
    this.timer = setInterval(() => onFlush(this.flush()), intervalMs);
  }

  enqueue(point: TelemetryPoint): void {
    this.pending.push(point);
  }

  flush(): TelemetryPoint[] {
    const batch = this.pending;
    this.pending = [];
    return batch;
  }

  dispose(): void {
    clearInterval(this.timer);
  }
}
