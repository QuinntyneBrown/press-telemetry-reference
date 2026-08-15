import type { Locator, Page } from '@playwright/test';

/** Header connection indicator — role=status inside the banner landmark.
 *  States render as exactly: Connected | Connecting | Reconnecting | Disconnected. */
export class ConnectionStatus {
  readonly indicator: Locator;

  constructor(page: Page) {
    this.indicator = page.getByRole('banner').getByRole('status');
  }
}

export class Tile {
  constructor(readonly root: Locator) {}

  value(): Locator {
    return this.root.getByTestId('tile-value');
  }

  timestamp(): Locator {
    return this.root.getByTestId('tile-timestamp');
  }

  pulse(): Locator {
    return this.root.getByTestId('live-pulse');
  }
}

export class ChartCard {
  constructor(readonly root: Locator) {}

  chart(): Locator {
    return this.root.getByRole('img');
  }

  line(): Locator {
    return this.root.getByTestId('series-line');
  }

  liveTag(): Locator {
    return this.root.getByText('Live', { exact: true });
  }

  pausedTag(): Locator {
    return this.root.getByText('paused', { exact: true });
  }

  async pointCount(): Promise<number> {
    const pts = await this.line().getAttribute('points');
    return pts ? pts.trim().split(/\s+/).length : 0;
  }

  /** HTML x-axis labels (aria-hidden, so unreachable by role). */
  xLabels(): Locator {
    return this.root.locator('.xlabels span');
  }
}
