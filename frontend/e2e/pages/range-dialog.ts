import type { Locator, Page } from '@playwright/test';

export class RangeDialog {
  readonly root: Locator;

  constructor(page: Page) {
    this.root = page.getByRole('dialog', { name: 'Select time range' });
  }

  fromInput(): Locator {
    return this.root.getByLabel('From (UTC)');
  }

  toInput(): Locator {
    return this.root.getByLabel('To (UTC)');
  }

  hint(): Locator {
    return this.root.getByText(/Maximum window: 24 hours/);
  }

  error(): Locator {
    return this.root.getByRole('alert');
  }

  applyButton(): Locator {
    return this.root.getByRole('button', { name: 'Apply' });
  }

  cancelButton(): Locator {
    return this.root.getByRole('button', { name: 'Cancel' });
  }
}
