import { useContainerSize } from './useContainerSize';

export interface SeriesPoint {
  timestamp: string;
  value: number;
}

export interface TimeSeriesChartProps {
  /** Accessible name for the chart; starts with "{deviceId} {metric}". */
  label: string;
  points: ReadonlyArray<SeriesPoint>;
  /** CSS color for the series stroke, e.g. 'var(--color-data-1)'. Fixed assignment, never cycled. */
  color?: string;
}

const formatValue = (v: number): string => (Number.isInteger(v) ? String(v) : v.toFixed(1));
const formatHm = (ms: number): string => new Date(ms).toISOString().slice(11, 16);

/**
 * SVG plot stretched via viewBox 0 0 100 100 + preserveAspectRatio="none" with
 * non-scaling strokes; axis labels are HTML so text never scales with the plot.
 * The x-label count follows the container width (redraw-on-resize, L2-016 AC4).
 */
export function TimeSeriesChart({ label, points, color = 'var(--color-data-1)' }: TimeSeriesChartProps) {
  const { ref, size } = useContainerSize<HTMLDivElement>();

  const times = points.map(p => Date.parse(p.timestamp));
  const values = points.map(p => p.value);
  const tMin = Math.min(...times);
  const tMax = Math.max(...times);
  const vMin = Math.min(...values);
  const vMax = Math.max(...values);
  const x = (t: number): number => (tMax === tMin ? 50 : ((t - tMin) / (tMax - tMin)) * 100);
  const y = (v: number): number => (vMax === vMin ? 50 : 100 - ((v - vMin) / (vMax - vMin)) * 100);
  const polyline = points.map(p => `${x(Date.parse(p.timestamp))},${y(p.value)}`).join(' ');

  const xLabelCount = Math.max(2, Math.round(size.width / 240) + 1);
  const xLabels =
    points.length === 0
      ? []
      : Array.from({ length: xLabelCount }, (_, i) =>
          formatHm(tMin + ((tMax - tMin) * i) / (xLabelCount - 1)),
        );

  return (
    <div className="chartbox" ref={ref}>
      <div className="ylabels" aria-hidden="true">
        {points.length > 0 && (
          <>
            <span>{formatValue(vMax)}</span>
            <span>{formatValue((vMin + vMax) / 2)}</span>
            <span>{formatValue(vMin)}</span>
          </>
        )}
      </div>
      <svg className="chart" viewBox="0 0 100 100" preserveAspectRatio="none" role="img" aria-label={label}>
        <line className="gridline" x1="0" y1="0" x2="100" y2="0" />
        <line className="gridline" x1="0" y1="50" x2="100" y2="50" />
        <line className="gridline" x1="0" y1="100" x2="100" y2="100" />
        {points.length > 0 && (
          <polyline className="series" data-testid="series-line" points={polyline} style={{ stroke: color }} />
        )}
      </svg>
      <div className="xlabels" aria-hidden="true">
        {points.length > 0 && xLabels.map((l, i) => <span key={i}>{l}</span>)}
      </div>
    </div>
  );
}
