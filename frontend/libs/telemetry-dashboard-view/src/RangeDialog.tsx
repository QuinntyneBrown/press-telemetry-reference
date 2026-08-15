import { useState } from 'react';
import type { TimeRange } from './data/types';

const MAX_WINDOW_MS = 24 * 60 * 60_000;
const INPUT_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/;

const toInputValue = (d: Date): string => d.toISOString().slice(0, 16);

function parseUtc(text: string): Date | null {
  if (!INPUT_PATTERN.test(text)) return null;
  const ms = Date.parse(`${text}:00.000Z`);
  return Number.isNaN(ms) ? null : new Date(ms);
}

export function validateRange(fromText: string, toText: string): { error?: string; range?: TimeRange } {
  const from = parseUtc(fromText);
  const to = parseUtc(toText);
  if (!from || !to) return { error: 'Times must be YYYY-MM-DDTHH:MM (UTC).' };
  if (from.getTime() >= to.getTime()) return { error: "'From' must be earlier than 'To'." };
  const span = to.getTime() - from.getTime();
  if (span > MAX_WINDOW_MS) {
    return { error: `Range spans ${Math.ceil(span / 3_600_000)} hours — maximum window is 24 hours.` };
  }
  return { range: { from, to } };
}

export interface RangeDialogProps {
  initial: TimeRange;
  onApply: (range: TimeRange) => void;
  onCancel: () => void;
}

export function RangeDialog({ initial, onApply, onCancel }: RangeDialogProps) {
  const [fromText, setFromText] = useState(toInputValue(initial.from));
  const [toText, setToText] = useState(toInputValue(initial.to));
  const { error, range } = validateRange(fromText, toText);
  const invalid = error !== undefined;

  return (
    <div className="scrim">
      <div className="dialog" role="dialog" aria-modal="true" aria-labelledby="range-dialog-title">
        <h2 className="dialog__title" id="range-dialog-title">
          Select time range
        </h2>
        <div className="field">
          <label htmlFor="range-from">From (UTC)</label>
          <input
            id="range-from"
            type="text"
            value={fromText}
            onChange={e => setFromText(e.target.value)}
            aria-invalid={invalid || undefined}
            aria-describedby={invalid ? 'range-dialog-error' : undefined}
          />
        </div>
        <div className="field">
          <label htmlFor="range-to">To (UTC)</label>
          <input
            id="range-to"
            type="text"
            value={toText}
            onChange={e => setToText(e.target.value)}
            aria-invalid={invalid || undefined}
            aria-describedby={invalid ? 'range-dialog-error' : undefined}
          />
        </div>
        <p className="dialog__hint">Maximum window: 24 hours. Times are UTC (ISO-8601).</p>
        {error && (
          <p className="dialog__error" id="range-dialog-error" role="alert">
            {error}
          </p>
        )}
        <div className="dialog__actions">
          <button className="btn" onClick={onCancel}>
            Cancel
          </button>
          <button className="btn btn--primary" disabled={invalid} onClick={() => range && onApply(range)}>
            Apply
          </button>
        </div>
      </div>
    </div>
  );
}
