import type { AuditTraceVoiceDto } from '../../api/generated/model';

/**
 * "The Score" (D-064): the voices of one correlation id laid out as a horizontal timeline, one lane per voice.
 * Pure: milliseconds in, SVG coordinates out, so the geometry is unit-tested without a browser.
 *
 * A command (an audit row) is drawn twice: the API lane carries its HTTP method and route, the Handler/Audit lane its
 * action and outcome. A failed command stores how long the pipeline took, so its API bar ends at the audit row and
 * starts that many ms earlier; 0 ms is the earliest moment any row records.
 */
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

/** The steps in order, before any geometry: each command becomes an API step and a Handler/Audit step. Times are relative to the earliest. */
export function scoreSteps(voices: readonly AuditTraceVoiceDto[]): ScoreStep[] {
  const raw: ScoreStep[] = [];
  for (const v of voices) {
    const tone = toneOf(v);
    const end = v.endMs ?? v.atMs;
    if (v.kind === 'command') {
      raw.push({
        key: `${v.id}:api`,
        lane: 'api',
        laneLabel: LABEL.api,
        title: v.detail ?? 'Request',
        detail: v.entityType ? `${v.entityType} ${v.entityId ?? ''}`.trim() : null,
        status: v.status,
        tone,
        startMs: v.atMs - (v.durationMs ?? 0),
        endMs: v.atMs,
      });
      raw.push({
        key: `${v.id}:audit`,
        lane: 'audit',
        laneLabel: LABEL.audit,
        title: v.label,
        detail: v.errorCode ?? null,
        status: v.status,
        tone,
        startMs: v.atMs,
        endMs: v.atMs,
      });
    } else if (v.kind === 'event') {
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
  };
}
