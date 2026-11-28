import type { AuditTraceVoiceDto } from '../../api/generated/model';

/**
 * "The Score" (D-064): the voices of one correlation id laid out as a horizontal timeline, one lane per voice.
 * Pure: milliseconds in, SVG coordinates out, so the geometry is unit-tested without a browser.
 *
 * The audit log writes one row per entity a command changed, all with the same action and route. Those rows are ONE
 * request, so they are grouped into one command (same action + same method/route, within COMMAND_GAP_MS of the previous
 * row of that group) and drawn twice: the API lane carries the HTTP method and route, the Handler/Audit lane the action
 * and the entities it touched. A failed command stores how long the pipeline took, so its API bar ends at the audit row
 * and starts that many ms earlier; 0 ms is the earliest moment any row records.
 */
/** Rows of one action and route further apart than this are separate requests. */
export const COMMAND_GAP_MS = 250;

export type ScoreLane = 'api' | 'audit' | 'event' | 'webhook';
export type ScoreTone = 'ok' | 'warn' | 'err';

export const SCORE_LANES: readonly { id: ScoreLane; label: string }[] = [
  { id: 'api', label: 'API' },
  { id: 'audit', label: 'Handler / Audit' },
  { id: 'event', label: 'Event' },
  { id: 'webhook', label: 'Webhook' },
];

export interface ScoreStep {
  key: string;
  lane: ScoreLane;
  laneLabel: string;
  title: string;
  detail: string | null;
  status: string;
  tone: ScoreTone;
  startMs: number;
  endMs: number;
  /** Command steps only: the audit rows grouped into this request, one entry per entity row. */
  rows?: { id: string; entityType: string | null; entityId: string | null; status: string; tone: ScoreTone }[];
  /** Command steps only: e.g. "5 rows: User, 4 × UserRole". */
  rowSummary?: string | null;
}

export interface ScoreBar extends ScoreStep {
  x: number;
  y: number;
  width: number;
}

export interface ScoreLayout {
  width: number;
  height: number;
  /** Lane label rows (y is the lane centre). */
  lanes: { id: ScoreLane; label: string; y: number }[];
  bars: ScoreBar[];
  /** Dashed connectors from the end of one step to the start of the next, in order. */
  connectors: { x1: number; y1: number; x2: number; y2: number }[];
  ticks: { x: number; ms: number }[];
  axisY: number;
  spanMs: number;
  steps: ScoreStep[];
  /** What the steps are made of, for the summary line. */
  counts: ScoreCounts;
}

export interface ScoreCounts {
  requests: number;
  auditRows: number;
  events: number;
  deliveries: number;
}

export interface ScoreGeometry {
  width: number;
  gutter: number;
  padRight: number;
  laneHeight: number;
  barHeight: number;
  minBar: number;
  axisHeight: number;
}

export const SCORE_GEOMETRY: ScoreGeometry = {
  width: 760,
  gutter: 128,
  padRight: 24,
  laneHeight: 40,
  barHeight: 14,
  minBar: 6,
  axisHeight: 28,
};

const LABEL: Record<ScoreLane, string> = Object.fromEntries(SCORE_LANES.map((l) => [l.id, l.label])) as Record<
  ScoreLane,
  string
>;

export function toneOf(voice: Pick<AuditTraceVoiceDto, 'kind' | 'status'>): ScoreTone {
  if (voice.kind === 'command') return voice.status === 'success' ? 'ok' : 'err';
  if (voice.status === 'dead') return 'err';
  return voice.status === 'dispatched' || voice.status === 'succeeded' ? 'ok' : 'warn';
}

interface CommandGroup {
  key: string;
  voices: AuditTraceVoiceDto[];
  lastAt: number;
}

/** "User, 4 × UserRole": entity types in first-seen order with their row counts. */
function entitySummary(voices: readonly AuditTraceVoiceDto[]): string {
  const counts = new Map<string, number>();
  for (const v of voices) {
    const t = v.entityType ?? 'unknown entity';
    counts.set(t, (counts.get(t) ?? 0) + 1);
  }
  const parts = [...counts].map(([t, n]) => (n === 1 ? t : `${n} × ${t}`));
  return `${voices.length} row${voices.length === 1 ? '' : 's'}: ${parts.join(', ')}`;
}

/** The steps in order, before any geometry: each command (one request) becomes an API step and a Handler/Audit step. Times are relative to the earliest. */
export function scoreSteps(voices: readonly AuditTraceVoiceDto[]): ScoreStep[] {
  const groups = new Map<AuditTraceVoiceDto, CommandGroup>(); // first voice of a group -> its group
  const open = new Map<string, CommandGroup>();
  for (const v of voices) {
    if (v.kind !== 'command') continue;
    const key = `${v.label}\u0000${v.detail ?? ''}`;
    const g = open.get(key);
    if (g && v.atMs - g.lastAt <= COMMAND_GAP_MS) {
      g.voices.push(v);
      g.lastAt = v.atMs;
    } else {
      const ng: CommandGroup = { key, voices: [v], lastAt: v.atMs };
      open.set(key, ng);
      groups.set(v, ng);
    }
  }

  const raw: ScoreStep[] = [];
  for (const v of voices) {
    const end = v.endMs ?? v.atMs;
    if (v.kind === 'command') {
      const g = groups.get(v);
      if (!g) continue; // a later row of a group already drawn
      const rows = g.voices.map((r) => ({
        id: r.id,
        entityType: r.entityType ?? null,
        entityId: r.entityId ?? null,
        status: r.status,
        tone: toneOf(r),
      }));
      const worst = g.voices.find((r) => toneOf(r) === 'err') ?? v;
      const tone = toneOf(worst);
      const first = Math.min(...g.voices.map((r) => r.atMs));
      const last = Math.max(...g.voices.map((r) => r.atMs));
      const start = Math.min(...g.voices.map((r) => r.atMs - (r.durationMs ?? 0)));
      const single = g.voices.length === 1;
      const entity = v.entityType ? `${v.entityType} ${v.entityId ?? ''}`.trim() : null;
      raw.push({
        key: `${v.id}:api`,
        lane: 'api',
        laneLabel: LABEL.api,
        title: v.detail ?? 'Request',
        detail: single ? entity : null,
        status: worst.status,
        tone,
        startMs: start,
        endMs: last,
      });
      raw.push({
        key: `${v.id}:audit`,
        lane: 'audit',
        laneLabel: LABEL.audit,
        title: v.label,
        detail: worst.errorCode ?? null,
        status: worst.status,
        tone,
        startMs: first,
        endMs: last,
        rows,
        rowSummary: single ? null : entitySummary(g.voices),
      });
      continue;
    }
    const tone = toneOf(v);
    if (v.kind === 'event') {
      raw.push({
        key: `${v.id}:event`,
        lane: 'event',
        laneLabel: LABEL.event,
        title: v.label,
        detail: v.entityType ? `${v.entityType} ${v.entityId ?? ''}`.trim() : null,
        status: v.status,
        tone,
        startMs: v.atMs,
        endMs: end,
      });
    } else if (v.kind === 'delivery') {
      const code = v.statusCode != null ? `HTTP ${v.statusCode}` : null;
      const tries = v.attempts != null ? `${v.attempts} attempt${v.attempts === 1 ? '' : 's'}` : null;
      raw.push({
        key: `${v.id}:webhook`,
        lane: 'webhook',
        laneLabel: LABEL.webhook,
        title: v.detail ? `${v.label} to ${v.detail}` : v.label,
        detail: [code, tries].filter(Boolean).join(', ') || null,
        status: v.status,
        tone,
        startMs: v.atMs,
        endMs: end,
      });
    }
  }
  const origin = Math.min(0, ...raw.map((s) => s.startMs));
  return raw.map((s) => ({ ...s, startMs: s.startMs - origin, endMs: Math.max(s.endMs, s.startMs) - origin }));
}

export function scoreCounts(voices: readonly AuditTraceVoiceDto[], steps: readonly ScoreStep[]): ScoreCounts {
  return {
    requests: steps.filter((s) => s.lane === 'api').length,
    auditRows: voices.filter((v) => v.kind === 'command').length,
    events: steps.filter((s) => s.lane === 'event').length,
    deliveries: steps.filter((s) => s.lane === 'webhook').length,
  };
}

/** A 1-2-5 step that gives four to six intervals over the span. */
export function tickStep(spanMs: number): number {
  if (spanMs <= 0) return 1;
  const rough = spanMs / 5;
  const pow = 10 ** Math.floor(Math.log10(rough));
  const unit = rough / pow;
  return (unit <= 1 ? 1 : unit <= 2 ? 2 : unit <= 5 ? 5 : 10) * pow;
}

export function layoutScore(voices: readonly AuditTraceVoiceDto[], g: ScoreGeometry = SCORE_GEOMETRY): ScoreLayout {
  const steps = scoreSteps(voices);
  const spanMs = steps.reduce((m, s) => Math.max(m, s.endMs), 0);
  const domain = spanMs > 0 ? spanMs : 1;
  const plot = g.width - g.gutter - g.padRight;
  const x = (ms: number) => g.gutter + (ms / domain) * plot;
  const laneY = (lane: ScoreLane) => SCORE_LANES.findIndex((l) => l.id === lane) * g.laneHeight + g.laneHeight / 2;

  const bars: ScoreBar[] = steps.map((s) => {
    const x0 = x(s.startMs);
    const width = Math.max(g.minBar, x(s.endMs) - x0);
    // A bar that would run past the plot is pulled back so it stays inside.
    return { ...s, x: Math.min(x0, g.width - g.padRight - width), y: laneY(s.lane) - g.barHeight / 2, width };
  });

  const connectors = bars.slice(1).map((b, i) => {
    const prev = bars[i];
    return { x1: prev.x + prev.width, y1: laneY(prev.lane), x2: b.x, y2: laneY(b.lane) };
  });

  const step = tickStep(spanMs);
  const ticks: { x: number; ms: number }[] = [];
  for (let ms = 0; ms <= spanMs + 1e-9; ms += step) ticks.push({ x: x(ms), ms });
  if (ticks.length === 0) ticks.push({ x: x(0), ms: 0 });

  const axisY = SCORE_LANES.length * g.laneHeight;
  return {
    width: g.width,
    height: axisY + g.axisHeight,
    lanes: SCORE_LANES.map((l) => ({ ...l, y: laneY(l.id) })),
    bars,
    connectors,
    ticks,
    axisY,
    spanMs,
    steps,
    counts: scoreCounts(voices, steps),
  };
}
