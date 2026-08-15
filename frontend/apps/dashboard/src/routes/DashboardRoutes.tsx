import { lazy } from 'react';
import { Route, Routes } from 'react-router';
import { ViewLoader } from './ViewLoader';

const OverviewView = lazy(() =>
  import('@press/telemetry-dashboard-view').then(m => ({ default: m.TelemetryDashboardView })),
);
const SeriesDetailView = lazy(() =>
  import('@press/telemetry-dashboard-view').then(m => ({ default: m.TelemetrySeriesDetailView })),
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
      <Route
        path="/series/:deviceId/:metric"
        element={
          <ViewLoader>
            <SeriesDetailView />
          </ViewLoader>
        }
      />
    </Routes>
  );
}
