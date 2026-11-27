import { ClaimDto } from '../../api/models';
import { isOverdue, slaDue } from './sla';
import { Tone } from './claims-logic';

const TONE_COLOR: Record<Tone, string> = {
  ok: 'var(--ok)',
  warn: 'var(--warn)',
  err: 'var(--err)',
  acc: 'var(--accent)',
  muted: 'var(--muted)',
};

export interface CalendarEvent {
  id: string;
  title: string;
  start: string;
  /** A token reference (`var(--ok)`): FullCalendar sets it as the event's colour variable. */
  color: string;
  /** Space-separated, FullCalendar v7's `className`. */
  className: string;
}

/** One event per open claim on its due instant; the class carries the status tone (colours live in tokens, see claims-calendar.css). */
export function calendarEvents(
  claims: readonly ClaimDto[],
  tones: Record<ClaimDto['status'], Tone>,
  now = Date.now(),
): CalendarEvent[] {
  const events: CalendarEvent[] = [];
  for (const claim of claims) {
    const due = slaDue(claim);
    if (!due) continue;
    events.push({
      id: claim.id,
      title: `${claim.number} · ${due.label}`,
      start: due.due,
      color: TONE_COLOR[tones[claim.status]],
      className: [
        'sla-ev',
        `sla-${tones[claim.status]}`,
        `sla-kind-${due.kind}`,
        ...(isOverdue(due.due, now) ? ['sla-overdue'] : []),
      ].join(' '),
    });
  }
  return events;
}
