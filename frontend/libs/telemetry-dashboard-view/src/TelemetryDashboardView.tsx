import { useState } from 'react';
import { Link } from 'react-router';
import { DashboardGrid, LatestValueTile, TimeSeriesChart, type ConnectionState } from '@press/dashboard-core';
import { AppHeader } from './AppHeader';
import { isSeriesLive } from './data/liveness';
import { useLiveTelemetry } from './data/useLiveTelemetry';
import { useTelemetryRange } from './data/useTelemetryRange';
import { useTelemetrySnapshot } from './data/useTelemetrySnapshot';
import type { TimeRange } from './data/types';
import { asOfTimeLabel } from './format';
import { unitFor } from './units';

const SKELETON_TILES = 8;
const SKELETON_CARDS = 2;

function SkeletonTile() {
  return (
    <div className="tile">
      <div className="sk-bar sk-bar--label" />
      <div className="sk-bar sk-bar--value" />
      <div className="sk-bar sk-bar--ts" />
    </div>
  );
}

function SkeletonCard() {
  return (
    <div className="card">
      <div className="card__head">
        <div className="sk-bar sk-bar--label" style={{ width: '30%' }} />
      </div>
      <div className="sk-bar sk-bar--chart" />
    </div>
  );
}

function OverviewChartCard({
  deviceId,
  metric,
  index,
  connectionState,
}: {
  deviceId: string;
  metric: string;
  index: number;
  connectionState: ConnectionState;
}) {
  // Window computed once at mount; live points append via cache patching (L2-010).
  const [range] = useState<TimeRange>(() => {
    const now = Date.now();
    return { from: new Date(now - 5 * 60_000), to: new Date(now) };
  });
  const query = useTelemetryRange({ deviceId, metric }, range);

  return (
    <div className="card" data-testid="chart-card">
      <div className="card__head">
        <span className="label">
          {deviceId} · {metric}
        </span>
        <div className="card__spacer" />
        <span className="toolbar__range">last 5 min</span>
        {connectionState === 'Connected' ? (
          <span className="live-tag">Live</span>
        ) : (
          <span className="toolbar__range">paused</span>
        )}
      </div>
      <TimeSeriesChart
        label={`${deviceId} ${metric} over the last 5 minutes`}
        points={query.data ?? []}
        color={`var(--color-data-${index + 1})`}
      />
    </div>
  );
}

function ErrorPanel({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="panel panel--error">
      <svg
        className="panel__icon"
        width="24"
        height="24"
        viewBox="0 0 24 24"
        fill="none"
        stroke="var(--color-status-error)"
        strokeWidth="1.5"
        aria-hidden="true"
      >
        <path d="M12 3.5 22 20.5H2L12 3.5Z" />
        <line x1="12" y1="10" x2="12" y2="15" />
        <circle cx="12" cy="17.8" r="0.6" fill="var(--color-status-error)" />
      </svg>
      <h2 className="panel__title">Couldn&apos;t load telemetry</h2>
      <p className="panel__detail">
        <code>GET /api/telemetry/latest</code> failed. The dashboard will show current values once it
        loads.
      </p>
      <button className="btn btn--primary" onClick={onRetry}>
        Retry
      </button>
    </div>
  );
}

function EmptyPanel() {
  return (
    <div className="panel">
      <h2 className="panel__title">No telemetry yet</h2>
      <p className="panel__detail">
        Nothing has been stored for any series. Publish a sample point and it appears here within
        seconds:
      </p>
      <pre className="panel__code">{`mosquitto_pub -t telemetry/press-01/temperature \\
  -m '{"deviceId":"press-01","metric":"temperature","value":87.4,"timestamp":"2026-08-15T12:00:00Z"}'`}</pre>
    </div>
  );
}

export function TelemetryDashboardView() {
  const connectionState = useLiveTelemetry();
  const snapshot = useTelemetrySnapshot();
  const points = snapshot.data ?? [];
  const deviceCount = new Set(points.map(p => p.deviceId)).size;

  return (
    <>
      <AppHeader connectionState={connectionState} />
      <div className="toolbar">
        <h1 className="toolbar__title">Overview</h1>
        <div className="toolbar__spacer" />
        {points.length > 0 &&
          (connectionState === 'Connected' ? (
            <span className="toolbar__range" data-testid="range-label">
              {deviceCount} devices · {points.length} series
            </span>
          ) : (
            <span className="toolbar__range">live paused — values as of {asOfTimeLabel(points)}</span>
          ))}
      </div>
      <main className="main">
        {snapshot.isPending ? (
          <DashboardGrid busy>
            {Array.from({ length: SKELETON_TILES }, (_, i) => (
              <SkeletonTile key={i} />
            ))}
            {Array.from({ length: SKELETON_CARDS }, (_, i) => (
              <SkeletonCard key={i} />
            ))}
          </DashboardGrid>
        ) : snapshot.isError ? (
          <DashboardGrid>
            <ErrorPanel onRetry={() => void snapshot.refetch()} />
          </DashboardGrid>
        ) : points.length === 0 ? (
          <DashboardGrid>
            <EmptyPanel />
          </DashboardGrid>
        ) : (
          <DashboardGrid>
            {points.map(p => (
              <Link
                key={`${p.deviceId}/${p.metric}`}
                to={`/series/${p.deviceId}/${p.metric}`}
                className="tile-link"
              >
                <LatestValueTile
                  label={p.metric}
                  device={p.deviceId}
                  value={p.value}
                  unit={unitFor(p.metric)}
                  timestamp={p.timestamp}
                  isLive={connectionState === 'Connected' && isSeriesLive(p)}
                  testId={`tile-${p.deviceId}-${p.metric}`}
                />
              </Link>
            ))}
            {points.slice(0, 2).map((p, i) => (
              <OverviewChartCard
                key={`chart-${p.deviceId}/${p.metric}`}
                deviceId={p.deviceId}
                metric={p.metric}
                index={i}
                connectionState={connectionState}
              />
            ))}
          </DashboardGrid>
        )}
      </main>
    </>
  );
}
