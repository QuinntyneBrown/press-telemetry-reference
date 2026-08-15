import { AppHeader } from './AppHeader';
import { useLiveTelemetry } from './data/useLiveTelemetry';

export function TelemetryDashboardView() {
  const connectionState = useLiveTelemetry();

  return (
    <>
      <AppHeader connectionState={connectionState} />
      <main className="main" />
    </>
  );
}
