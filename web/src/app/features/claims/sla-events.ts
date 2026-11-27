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

/** A local calendar day as `YYYY-MM-DD` (the day a person sees in the grid, not the UTC day). */
export function dayKey(d: Date): string {
  const two = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${two(d.getMonth() + 1)}-${two(d.getDate())}`;
}

export interface DueItem {
  id: string;
  number: string;
  status: ClaimDto['status'];
  /** What is due: "Pickup due", "Decision due"... */
  what: string;
  overdue: boolean;
}

/** The open claims whose next SLA falls on the given local day (`YYYY-MM-DD`), for the day popover. Empty means "Nothing due". */
export function claimsDueOn(claims: readonly ClaimDto[], day: string, now = Date.now()): DueItem[] {
  const items: DueItem[] = [];
  for (const claim of claims) {
    const due = slaDue(claim);
    if (!due || dayKey(new Date(due.due)) !== day) continue;
    items.push({
      id: claim.id,
      number: claim.number,
      status: claim.status,
      what: due.label,
      overdue: isOverdue(due.due, now),
    });
  }
  return items;
}
