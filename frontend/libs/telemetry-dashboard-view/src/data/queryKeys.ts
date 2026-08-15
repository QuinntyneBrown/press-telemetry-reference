export const telemetryKeys = {
  all: ['telemetry'] as const,
  latest: ['telemetry', 'latest'] as const,
  range: (deviceId: string, metric: string, fromIso: string, toIso: string) =>
    ['telemetry', 'range', deviceId, metric, fromIso, toIso] as const,
};
