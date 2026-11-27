import type { AuditTraceVoiceDto } from '../../api/generated/model';
import { layoutScore, SCORE_GEOMETRY, SCORE_LANES, scoreSteps, tickStep, toneOf } from './score-layout';

function voice(v: Partial<AuditTraceVoiceDto> & Pick<AuditTraceVoiceDto, 'kind' | 'atMs'>): AuditTraceVoiceDto {
  return { id: `${v.kind}-${v.atMs}`, at: '2026-11-28T00:00:00Z', label: 'x', status: 'success', ...v };
}

const reserve: AuditTraceVoiceDto[] = [
  voice({
    kind: 'command',
    atMs: 0,
    label: 'claims.reserve',
    detail: 'PUT api/v1/claims/{id}/reserve',
    status: 'success',
  }),
  voice({ kind: 'event', atMs: 0, endMs: 40, label: 'claim.reserve.set.v1', status: 'dispatched' }),
  voice({
    kind: 'delivery',
    atMs: 40,
    endMs: 100,
    label: 'claim.reserve.set.v1',
    detail: 'crm',
    status: 'succeeded',
    statusCode: 200,
    attempts: 1,
  }),
];

describe('score layout', () => {
  it('puts each voice in its lane, a command in both the API and the Handler/Audit lane', () => {
    const steps = scoreSteps(reserve);

    expect(steps.map((s) => s.lane)).toEqual(['api', 'audit', 'event', 'webhook']);
    expect(steps[0].title).toBe('PUT api/v1/claims/{id}/reserve');
    expect(steps[1].title).toBe('claims.reserve');
    expect(steps[3].title).toBe('claim.reserve.set.v1 to crm');
    expect(steps[3].detail).toBe('HTTP 200, 1 attempt');
  });

  it('maps milliseconds linearly onto the plot between the gutter and the right pad', () => {
    const g = SCORE_GEOMETRY;
    const plot = g.width - g.gutter - g.padRight;
    const layout = layoutScore(reserve);
    const [api, audit, event, hook] = layout.bars;

    expect(layout.spanMs).toBe(100);
    expect(api.x).toBe(g.gutter);
    expect(audit.width).toBe(g.minBar); // a point in time still gets a visible mark
    expect(event.x).toBe(g.gutter);
    expect(event.width).toBeCloseTo((40 / 100) * plot);
    expect(hook.x).toBeCloseTo(g.gutter + 0.4 * plot);
    expect(hook.x + hook.width).toBeCloseTo(g.width - g.padRight);
  });

  it('centres each bar on its lane, in the order of SCORE_LANES', () => {
    const g = SCORE_GEOMETRY;
    const layout = layoutScore(reserve);

    expect(layout.lanes.map((l) => l.id)).toEqual(SCORE_LANES.map((l) => l.id));
    layout.bars.forEach((b) => {
      const lane = layout.lanes.find((l) => l.id === b.lane)!;
      expect(b.y + g.barHeight / 2).toBe(lane.y);
    });
    expect(layout.axisY).toBe(SCORE_LANES.length * g.laneHeight);
  });

  it('joins consecutive steps with one connector each, end of one to start of the next', () => {
    const layout = layoutScore(reserve);

    expect(layout.connectors).toHaveLength(layout.bars.length - 1);
    const [, second] = layout.connectors;
    const [, audit, event] = layout.bars;
    expect(second).toEqual({ x1: audit.x + audit.width, y1: audit.y + 7, x2: event.x, y2: event.y + 7 });
  });

  it('starts a failed command’s API bar its duration before the audit row and moves 0 ms there', () => {
    const steps = scoreSteps([
      voice({ kind: 'command', atMs: 0, status: 'failure', durationMs: 12, errorCode: 'claim.invalid_transition' }),
    ]);

    expect(steps.map((s) => [s.lane, s.startMs, s.endMs])).toEqual([
      ['api', 0, 12],
      ['audit', 12, 12],
    ]);
    expect(steps[1].detail).toBe('claim.invalid_transition');
    expect(steps.every((s) => s.tone === 'err')).toBe(true);
  });

  it('labels the axis in 1-2-5 steps from 0 ms, and a trace with no span gets a single 0 tick', () => {
    expect(tickStep(100)).toBe(20);
    expect(tickStep(40)).toBe(10);
    expect(tickStep(1300)).toBe(500);
    expect(layoutScore(reserve).ticks.map((t) => t.ms)).toEqual([0, 20, 40, 60, 80, 100]);
    expect(layoutScore([voice({ kind: 'command', atMs: 0 })]).ticks.map((t) => t.ms)).toEqual([0]);
  });

  it('tones by outcome: success and delivered are ok, waiting is warn, failure and dead are err', () => {
    expect(toneOf({ kind: 'command', status: 'success' })).toBe('ok');
    expect(toneOf({ kind: 'command', status: 'failure' })).toBe('err');
    expect(toneOf({ kind: 'event', status: 'pending' })).toBe('warn');
    expect(toneOf({ kind: 'delivery', status: 'in_flight' })).toBe('warn');
    expect(toneOf({ kind: 'delivery', status: 'dead' })).toBe('err');
    expect(toneOf({ kind: 'delivery', status: 'succeeded' })).toBe('ok');
  });
});
