import { Pipe, PipeTransform } from '@angular/core';

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 31_536_000],
  ['month', 2_592_000],
  ['week', 604_800],
  ['day', 86_400],
  ['hour', 3_600],
  ['minute', 60],
];
const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto', style: 'short' });

export function relativeTime(iso: string | null | undefined, now = Date.now()): string {
  if (!iso) return '';
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000);
  const abs = Math.abs(seconds);
  if (abs < 45) return seconds <= 0 ? 'just now' : 'in a moment';
  for (const [unit, size] of UNITS) {
    if (abs >= size) return rtf.format(Math.round(seconds / size), unit);
  }
  return rtf.format(Math.round(seconds / 60), 'minute');
}

/** "2 min. ago"; pure on purpose: lists re-render on refetch, which is often enough for a console. */
@Pipe({ name: 'relative' })
export class RelativeTimePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return relativeTime(value);
  }
}

const dtf = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'medium' });
@Pipe({ name: 'stamp' })
export class StampPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? dtf.format(new Date(value)) : '';
  }
}

/** "ada.lovelace@example.test" -> "a•••@example.test" (display only; the API still holds the full value). */
export function maskEmail(email: string): string {
  const at = email.indexOf('@');
  if (at < 1) return '•••';
  return `${email[0]}•••${email.slice(at)}`;
}

@Pipe({ name: 'maskEmail' })
export class MaskEmailPipe implements PipeTransform {
  transform(value: string | null | undefined, enabled = true): string {
    if (!value) return '';
    return enabled ? maskEmail(value) : value;
  }
}

/** API-side masking writes `***`; show it as bullets so masked values read as masked, not as data. */
export function displayMasked(value: unknown): string {
  if (value === null || value === undefined) return '∅';
  const text = typeof value === 'string' ? value : JSON.stringify(value);
  return text.replace(/\*{3,}/g, '•••');
}
