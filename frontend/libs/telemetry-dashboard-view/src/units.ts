// The API carries no unit field (pinned contract); units are presentation
// vocabulary owned by the view, keyed by metric name.
export const METRIC_UNITS: Record<string, string> = {
  temperature: 'degC',
  pressure: 'bar',
  vibration: 'mm/s',
  cycle_count: 'cycles',
};

export const unitFor = (metric: string): string => METRIC_UNITS[metric] ?? '';
