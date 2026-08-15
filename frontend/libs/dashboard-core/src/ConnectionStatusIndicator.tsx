import type { ConnectionState } from './ConnectionState';

const VARIANT: Record<ConnectionState, string> = {
  Connected: 'conn--ok',
  Connecting: 'conn--warn',
  Reconnecting: 'conn--warn',
  Disconnected: 'conn--err',
};

export interface ConnectionStatusIndicatorProps {
  state: ConnectionState;
}

export function ConnectionStatusIndicator({ state }: ConnectionStatusIndicatorProps) {
  return (
    <div className={`conn ${VARIANT[state]}`} role="status">
      <span className="conn__dot" />
      {state}
    </div>
  );
}
