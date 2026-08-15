/** Wire shape of one telemetry point (snapshot endpoint and hub messages). */
export interface TelemetryPoint {
  deviceId: string;
  metric: string;
  value: number;
  timestamp: string;
}

/** Wire shape of one range-endpoint point. */
export interface SeriesPoint {
  timestamp: string;
  value: number;
}

export interface SeriesKey {
  deviceId: string;
  metric: string;
}

export interface TimeRange {
  from: Date;
  to: Date;
}

export const seriesKeyOf = (p: SeriesKey): string => `${p.deviceId}/${p.metric}`;
