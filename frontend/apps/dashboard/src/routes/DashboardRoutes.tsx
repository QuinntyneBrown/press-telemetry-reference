import { lazy } from 'react';
import { Route, Routes } from 'react-router';
import { ViewLoader } from './ViewLoader';

const OverviewView = lazy(() =>
  import('@press/telemetry-dashboard-view').then(m => ({ default: m.TelemetryDashboardView })),
);

export function DashboardRoutes() {
  return (
    <Routes>
      <Route
        path="/"
        element={
          <ViewLoader>
            <OverviewView />
          </ViewLoader>
        }
      />
    </Routes>
  );
}
