import type { Page } from '@playwright/test';

export interface TelemetryPoint {
  deviceId: string;
  metric: string;
  value: number;
  timestamp: string;
}

export interface SeriesPoint {
  timestamp: string;
  value: number;
}

const RANGE_URL = /\/api\/telemetry\/(?!latest(?:[/?]|$))[^/?]+\/[^/?]+(\?|$)/;
const IDENTIFIER = /^[A-Za-z0-9_-]{1,64}$/;
const MAX_RANGE_MS = 24 * 60 * 60 * 1000;

interface Problem {
  title: string;
  status: number;
  detail: string;
}

/**
 * Faithful mock of the Telemetry.Api REST contract (pinned by
 * backend/tests/Telemetry.IntegrationTests): camelCase JSON, ascending [from, to)
 * ranges, 400 application/problem+json for invalid parameters. If the client ever
 * sends a request it should have blocked client-side, tests fail loudly here
 * instead of passing on a lenient stub.
 */
export class TelemetryApiMock {
  private latest: TelemetryPoint[] = [];
  private ranges = new Map<string, SeriesPoint[]>();
  private latestMode: 'ok' | 'error' | 'hold' = 'ok';
  private releaseHold?: () => void;
  readonly calls = { latest: 0, range: [] as URL[] };

  constructor(private readonly page: Page) {}

  async install(): Promise<void> {
    await this.page.route('**/api/telemetry/latest', async route => {
      this.calls.latest += 1;
      if (this.latestMode === 'error') {
        return route.fulfill({
          status: 500,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Internal Server Error', status: 500 }),
        });
      }
      if (this.latestMode === 'hold') {
        await new Promise<void>(resolve => {
          this.releaseHold = resolve;
        });
      }
      await route.fulfill({ json: this.latest });
    });

    await this.page.route(RANGE_URL, async route => {
      const url = new URL(route.request().url());
      this.calls.range.push(url);
      const [, , , deviceId, metric] = url.pathname.split('/');
      const problem = this.validate(deviceId, metric, url.searchParams.get('from'), url.searchParams.get('to'));
      if (problem) {
        return route.fulfill({
          status: problem.status,
          contentType: 'application/problem+json',
          body: JSON.stringify(problem),
        });
      }
      const from = Date.parse(url.searchParams.get('from')!);
      const to = Date.parse(url.searchParams.get('to')!);
      const points = (this.ranges.get(`${deviceId}/${metric}`) ?? [])
        .filter(p => Date.parse(p.timestamp) >= from && Date.parse(p.timestamp) < to)
        .sort((a, b) => Date.parse(a.timestamp) - Date.parse(b.timestamp));
      await route.fulfill({ json: points });
    });
  }

  seedLatest(points: TelemetryPoint[]): this {
    this.latest = points;
    return this;
  }

  seedRange(deviceId: string, metric: string, points: SeriesPoint[]): this {
    this.ranges.set(`${deviceId}/${metric}`, points);
    return this;
  }

  failLatest(): this {
    this.latestMode = 'error';
    return this;
  }

  restoreLatest(): this {
    this.latestMode = 'ok';
    return this;
  }

  /** Park the next /latest response until releaseLatest() — deterministic loading states. */
  holdLatest(): this {
    this.latestMode = 'hold';
    return this;
  }

  releaseLatest(): void {
    this.latestMode = 'ok';
    this.releaseHold?.();
    this.releaseHold = undefined;
  }

  private validate(deviceId: string, metric: string, from: string | null, to: string | null): Problem | null {
    const bad = (detail: string): Problem => ({ title: 'Bad Request', status: 400, detail });
    if (!IDENTIFIER.test(deviceId)) return bad(`Route parameter 'deviceId' must match [A-Za-z0-9_-]{1,64}.`);
    if (!IDENTIFIER.test(metric)) return bad(`Route parameter 'metric' must match [A-Za-z0-9_-]{1,64}.`);
    if (from === null || Number.isNaN(Date.parse(from))) {
      return bad(`Query parameter 'from' must be a valid ISO-8601 timestamp.`);
    }
    if (to === null || Number.isNaN(Date.parse(to))) {
      return bad(`Query parameter 'to' must be a valid ISO-8601 timestamp.`);
    }
    if (Date.parse(from) >= Date.parse(to)) {
      return bad(`'from' must be earlier than 'to'.`);
    }
    if (Date.parse(to) - Date.parse(from) > MAX_RANGE_MS) {
      return bad(`Range must not exceed 24 hours.`);
    }
    return null;
  }
}
