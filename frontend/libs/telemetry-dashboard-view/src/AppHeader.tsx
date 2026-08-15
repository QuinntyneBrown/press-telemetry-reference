import { ConnectionStatusIndicator, type ConnectionState } from '@press/dashboard-core';

export function AppHeader({ connectionState }: { connectionState: ConnectionState }) {
  return (
    <header className="app-header">
      <div className="brand">Press Telemetry</div>
      <ConnectionStatusIndicator state={connectionState} />
    </header>
  );
}
