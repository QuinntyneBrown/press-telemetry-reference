// Base URL for REST and the hub. Empty string = same-origin (L2-018: the
// committed default; VITE_API_BASE_URL overrides at build/dev time).
export const apiBaseUrl: string = import.meta.env.VITE_API_BASE_URL ?? '';
export const hubUrl = `${apiBaseUrl}/hubs/telemetry`;

export const FLUSH_INTERVAL_MS = 1000; // ~1 Hz cache commit cadence (L2-011)
export const RETRY_DELAY_MS = 2000; // constant reconnect delay, retries never stop (L2-012)
export const LIVE_WINDOW_MS = 5000; // pulse decays when no hub point arrived within this window
