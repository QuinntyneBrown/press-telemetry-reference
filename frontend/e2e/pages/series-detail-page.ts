import type { Locator, Page } from '@playwright/test';
import { ChartCard, ConnectionStatus } from './components';
import { RangeDialog } from './range-dialog';

export class SeriesDetailPage {
  readonly connection: ConnectionStatus;

  constructor(readonly page: Page) {
    this.connection = new ConnectionStatus(page);
  }

  async goto(deviceId: string, metric: string): Promise<void> {
    await this.page.goto(`/series/${deviceId}/${metric}`);
  }

  backLink(): Locator {
    return this.page.getByRole('link', { name: '← Overview' });
  }

  title(deviceId: string, metric: string): Locator {
    return this.page.getByRole('heading', { name: `${deviceId} · ${metric}` });
  }

  chip(name: string): Locator {
    return this.page.getByRole('button', { name, exact: true });
  }

  customButton(): Locator {
    return this.page.getByRole('button', { name: 'Custom…' });
  }

  rangeLabel(): Locator {
    return this.page.getByTestId('range-label');
  }

  card(): ChartCard {
    return new ChartCard(this.page.locator('[data-testid="chart-card"]'));
  }

  currentValue(): Locator {
    return this.page.getByText(/^current /);
  }

  emptyOverlay(): Locator {
    return this.page.getByText('No data in this range');
  }

  async openRangeDialog(): Promise<RangeDialog> {
    await this.customButton().click();
    const dialog = new RangeDialog(this.page);
    await dialog.root.waitFor();
    return dialog;
  }
}
