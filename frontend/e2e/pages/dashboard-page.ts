import type { Locator, Page } from '@playwright/test';
import { ChartCard, ConnectionStatus, Tile } from './components';

export class DashboardPage {
  readonly connection: ConnectionStatus;

  constructor(readonly page: Page) {
    this.connection = new ConnectionStatus(page);
  }

  async goto(): Promise<void> {
    await this.page.goto('/');
  }

  brand(): Locator {
    return this.page.getByRole('banner').getByText('Press Telemetry');
  }

  title(): Locator {
    return this.page.getByRole('heading', { name: 'Overview' });
  }

  summary(): Locator {
    return this.page.getByTestId('range-label');
  }

  grid(): Locator {
    return this.page.getByTestId('dashboard-grid');
  }

  loadingGrid(): Locator {
    return this.page.locator('[data-testid="dashboard-grid"][aria-busy="true"]');
  }

  tile(deviceId: string, metric: string): Tile {
    return new Tile(this.page.getByTestId(`tile-${deviceId}-${metric}`));
  }

  tiles(): Locator {
    // Tile roots are tile-{deviceId}-{metric}; exclude the inner tile-value/tile-timestamp ids.
    return this.page.getByTestId(/^tile-(?!value$|timestamp$).+/);
  }

  chartCard(deviceId: string, metric: string): ChartCard {
    return new ChartCard(
      this.page
        .locator('[data-testid="chart-card"]')
        .filter({ has: this.page.getByRole('img', { name: new RegExp(`^${deviceId} ${metric}`) }) }),
    );
  }

  errorPanel(): Locator {
    return this.page.getByRole('heading', { name: "Couldn't load telemetry" });
  }

  retryButton(): Locator {
    return this.page.getByRole('button', { name: 'Retry' });
  }

  emptyPanel(): Locator {
    return this.page.getByRole('heading', { name: 'No telemetry yet' });
  }

  emptySnippet(): Locator {
    return this.page.getByText(/mosquitto_pub -t telemetry/);
  }

  pausedBanner(): Locator {
    return this.page.getByText(/live paused/);
  }

  /** Resolved column count of the auto-fit grid (browser resolves repeat(auto-fit, …)). */
  async columnCount(): Promise<number> {
    return this.grid().evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length);
  }

  async hasHorizontalScroll(): Promise<boolean> {
    return this.page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
  }
}
