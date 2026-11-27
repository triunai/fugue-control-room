import { MODULE_ENTITY_IDS, WHEN_OFF_FALLBACK, whenOff } from './when-off';

describe('whenOff', () => {
  it('describes every seeded module with its own sentence', () => {
    for (const code of Object.keys(MODULE_ENTITY_IDS)) expect(whenOff(code)).not.toBe(WHEN_OFF_FALLBACK);
  });

  it('falls back to a generic sentence for an unknown code', () => {
    expect(whenOff('BILLING')).toBe(WHEN_OFF_FALLBACK);
  });
});
