import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { TimeSeriesChart } from '@press/dashboard-core';
import { AppHeader } from './AppHeader';
import { RangeDialog } from './RangeDialog';
import { useLiveTelemetry } from './data/useLiveTelemetry';
import { useTelemetryRange } from './data/useTelemetryRange';
import { useTelemetrySnapshot } from './data/useTelemetrySnapshot';
import type { TimeRange } from './data/types';
import { formatRangeLabel } from './format';
import { unitFor } from './units';

type Preset = '1H' | '6H' | '24H' | 'Custom';

const PRESET_HOURS: Record<Exclude<Preset, 'Custom'>, number> = { '1H': 1, '6H': 6, '24H': 24 };

function presetRange(hours: number): TimeRange {
  const now = Date.now();
  return { from: new Date(now - hours * 60 * 60_000), to: new Date(now) };
}

export function TelemetrySeriesDetailView() {
  const { deviceId = '', metric = '' } = useParams();
  const connectionState = useLiveTelemetry();
  const snapshot = useTelemetrySnapshot();
  const [preset, setPreset] = useState<Preset>('24H');
  const [range, setRange] = useState<TimeRange>(() => presetRange(24));
  // Presets track "now" and keep receiving live appends; a custom window is fixed.
  const query = useTelemetryRange({ deviceId, metric }, range, { live: preset !== 'Custom' });
  const points = query.data ?? [];
  const current = snapshot.data?.find(p => p.deviceId === deviceId && p.metric === metric);
  const hasData = points.length > 0;
  const [dialogOpen, setDialogOpen] = useState(false);

  const selectPreset = (p: Exclude<Preset, 'Custom'>) => {
    setPreset(p);
    setRange(presetRange(PRESET_HOURS[p]));
  };

  return (
    <>
      <AppHeader connectionState={connectionState} />
      <div className="toolbar">
        <Link to="/">← Overview</Link>
        <h1 className="toolbar__title">
          {deviceId} · {metric}
        </h1>
        <div className="toolbar__spacer" />
        {(Object.keys(PRESET_HOURS) as Array<Exclude<Preset, 'Custom'>>).map(p => (
          <button
            key={p}
            className={`chip${preset === p ? ' chip--active' : ''}`}
            aria-pressed={preset === p}
            onClick={() => selectPreset(p)}
          >
            {p}
          </button>
        ))}
        {preset === 'Custom' && (
          <button className="chip chip--active" aria-pressed="true">
            Custom
          </button>
        )}
        <button className="btn" onClick={() => setDialogOpen(true)}>
          Custom…
        </button>
        <span className="toolbar__range" data-testid="range-label">
          {formatRangeLabel(range)}
        </span>
      </div>
      <main className="main">
        <div className="card" data-testid="chart-card">
          <div className="card__head">
            <span className="label">
              {deviceId} · {metric}
            </span>
            <div className="card__spacer" />
            {hasData && current && (
              <span className="toolbar__range">
                current {current.value} {unitFor(metric)}
              </span>
            )}
            {hasData &&
              (connectionState === 'Connected' ? (
                <span className="live-tag">Live</span>
              ) : (
                <span className="toolbar__range">paused</span>
              ))}
          </div>
          {!hasData && !query.isPending ? (
            <div className="plot">
              <TimeSeriesChart label={`${deviceId} ${metric} over the selected range`} points={[]} />
              <div className="plot__empty">
                <div className="plot__empty-title">No data in this range</div>
                <div className="plot__empty-range">{formatRangeLabel(range)}</div>
              </div>
            </div>
          ) : (
            <TimeSeriesChart
              label={`${deviceId} ${metric} over the selected range`}
              points={points}
              color="var(--color-data-1)"
            />
          )}
        </div>
      </main>
      {dialogOpen && (
        <RangeDialog
          initial={range}
          onCancel={() => setDialogOpen(false)}
          onApply={r => {
            setRange(r);
            setPreset('Custom');
            setDialogOpen(false);
          }}
        />
      )}
    </>
  );
}
