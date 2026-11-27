import { refetchesAfterToggle } from './modules-page';

describe('refetchesAfterToggle', () => {
  const lastChanged = ['modules', 'CLAIMS', 'last-changed', true];
  it('does not refetch Last changed when AUDIT is switched off', () => {
    expect(refetchesAfterToggle(lastChanged, { m: { code: 'AUDIT' }, on: false })).toBe(false);
  });
  it('still refetches the list, and Last changed for other switches', () => {
    expect(refetchesAfterToggle(['modules'], { m: { code: 'AUDIT' }, on: false })).toBe(true);
    expect(refetchesAfterToggle(lastChanged, { m: { code: 'AUDIT' }, on: true })).toBe(true);
    expect(refetchesAfterToggle(lastChanged, { m: { code: 'CUSTOMERS' }, on: false })).toBe(true);
  });
});
