export interface LatestValueTileProps {
  /** Metric name; rendered lowercase in the DOM, uppercased visually by CSS. */
  label: string;
  device: string;
  value: number;
  unit: string;
  timestamp: string;
  isLive: boolean;
  testId?: string;
}

export function LatestValueTile({ label, device, value, unit, timestamp, isLive, testId }: LatestValueTileProps) {
  return (
    <div className="tile" data-testid={testId}>
      <div className="tile__head">
        <span className="label">{label}</span>
        <span className="tile__device">{device}</span>
      </div>
      <div className="tile__value" data-testid="tile-value">
        {value} <span className="tile__unit">{unit}</span>
        {isLive && <span className="pulse" data-testid="live-pulse" aria-hidden="true" />}
      </div>
      <div className="tile__ts" data-testid="tile-timestamp">{timestamp}</div>
    </div>
  );
}
