import { visibleGroups } from './nav';

const CLAIMANT = ['CLAIMS_SUBMIT', 'CLAIMS_READ', 'CLAIMS_RESPOND'];
const STAFF = ['CLAIMS_READ', 'CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_WORKLOAD'];
const MANAGER = [...STAFF, 'CLAIMS_SETTLE', 'CLAIMS_EXPOSURE'];

/** What the sidebar shows for a permission set, as "Group > Item" labels. */
function navFor(permissions: string[]): string[] {
  const set = new Set(permissions);
  const has = (code: string | string[]) => (typeof code === 'string' ? set.has(code) : code.some((c) => set.has(c)));
  return visibleGroups(has)
    .filter((g) => g.label === 'Claims')
    .flatMap((g) => g.items.map((i) => `${g.label} > ${i.label}`));
}

describe('permission-driven navigation: Claims', () => {
  it('a claimant sees My claims, Report an incident and the SLA calendar, and nothing staff-only', () => {
    const nav = navFor(CLAIMANT);
    expect(nav).toEqual(['Claims > My claims', 'Claims > Report an incident', 'Claims > SLA calendar']);
  });

  it('staff see the queue and team page, not the claimant screens', () => {
    const nav = navFor(STAFF);
    expect(nav).toContain('Claims > Claims queue');
    expect(nav).toContain('Claims > Team and exposure');
    expect(nav).toContain('Claims > Reserve sheet');
    expect(nav).toContain('Claims > SLA calendar');
    expect(nav).not.toContain('Claims > My claims');
    expect(navFor(CLAIMANT)).not.toContain('Claims > Reserve sheet');
    expect(nav).not.toContain('Claims > Exposure');
  });

  it('a manager (CLAIMS_EXPOSURE) sees the team page too', () => {
    expect(navFor(MANAGER)).toContain('Claims > Team and exposure');
  });

  it('a user with no claims permission has no Claims group at all', () => {
    expect(navFor(['CUSTOMERS_VIEW'])).toEqual([]);
  });
});

describe('nav groups', () => {
  it('keeps the starter example (Customers) in its own last group, out of the claim groups', () => {
    const all = () => true;
    const groups = visibleGroups(all);
    expect(groups.at(-1)?.label).toBe('Starter example');
    expect(groups.at(-1)?.items.map((i) => i.label)).toEqual(['Customers']);
    expect(groups.find((g) => g.label === 'Data')?.items.map((i) => i.label)).toEqual(['Audit log']);
    expect(groups[0].label).toBe('Claims');
  });
});

describe('Fugue, explained', () => {
  it('is an external nav item every persona sees, claimant included', () => {
    const none = () => false;
    const item = visibleGroups(none)
      .flatMap((g) => g.items)
      .find((i) => i.label === 'Fugue, explained');
    expect(item).toMatchObject({ path: '/explained/index.html', external: true });
    expect(item?.permission).toBeUndefined();
  });
});
