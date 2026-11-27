import { PAGE_TOURS, personaOf, stepsFor, shouldAutoplay, suppressTourFromUrl, tourKeyFor, tourSuppressed } from './tour-steps';

const grant =
  (...codes: string[]) =>
  (code: string) =>
    codes.includes(code);

describe('personaOf', () => {
  it('picks the broadest persona from permission codes', () => {
    expect(personaOf(grant('CLAIMS_SUBMIT', 'CLAIMS_READ'))).toBe('claimant');
    expect(personaOf(grant('CLAIMS_HANDLE', 'CLAIMS_ALL'))).toBe('officer');
    expect(personaOf(grant('CLAIMS_HANDLE', 'CLAIMS_EXPOSURE'))).toBe('manager');
    expect(personaOf(grant('CLAIMS_EXPOSURE', 'MODULES_VIEW'))).toBe('admin');
    expect(personaOf(grant('CUSTOMERS_VIEW'))).toBe('member');
  });
});

describe('stepsFor', () => {
  it('opens every tour on the permission-driven navigation', () => {
    for (const p of ['claimant', 'officer', 'manager', 'admin', 'member'] as const) {
      expect(stepsFor(p)[0].selector).toBe('[data-tour="nav"]');
    }
  });

  it('covers the points each persona is meant to show', () => {
    const titles = (p: Parameters<typeof stepsFor>[0]) => stepsFor(p).map((s) => s.title);
    expect(titles('claimant')).toEqual(
      expect.arrayContaining(['Report an incident', 'The timeline', 'Answer the question']),
    );
    expect(titles('officer')).toEqual(
      expect.arrayContaining(['The queue at a glance', 'Tabs and filters', 'Only the actions valid for the status']),
    );
    expect(titles('manager')).toEqual(expect.arrayContaining(['Exposure per currency', 'Why there is no grand total']));
    expect(titles('admin')).toEqual(expect.arrayContaining(['The audit log', 'Module toggles', 'Access']));
  });

  it('keeps copy short: a title and at most two sentences per step', () => {
    for (const p of ['claimant', 'officer', 'manager', 'admin'] as const) {
      for (const s of stepsFor(p)) {
        expect(s.title.length).toBeLessThan(45);
        expect(s.description.split(/(?<=[.!?])\s/).length).toBeLessThanOrEqual(2);
      }
    }
  });
});

describe('page tours', () => {
  it('maps URLs to page keys', () => {
    expect(tourKeyFor('/claims/queue?status=submitted')).toBe('queue');
    expect(tourKeyFor('/claims/01a21c89-9940-73b6-91f0-3d61c3ecddd4')).toBe('claim-detail');
    expect(tourKeyFor('/claims/team')).toBe('team-exposure');
    expect(tourKeyFor('/claims/exposure')).toBe('team-exposure');
    expect(tourKeyFor('/audit')).toBe('landing');
  });

  it('registers the keys the claim UI calls', () => {
    expect(Object.keys(PAGE_TOURS)).toEqual(
      expect.arrayContaining(['claim-detail', 'queue', 'team-exposure', 'landing']),
    );
  });

  it('?tour=off silences auto-start for the session', () => {
    sessionStorage.clear();
    expect(tourSuppressed()).toBe(false);
    history.replaceState(null, '', '/?tour=off');
    suppressTourFromUrl();
    history.replaceState(null, '', '/');
    expect(tourSuppressed()).toBe(true);
    sessionStorage.clear();
  });

  it('autoplays only on the first, unsuppressed visit of a signed-in user', () => {
    const base = { suppressed: false, seen: false, signedIn: true };
    expect(shouldAutoplay(base)).toBe(true);
    expect(shouldAutoplay({ ...base, seen: true })).toBe(false);
    expect(shouldAutoplay({ ...base, suppressed: true })).toBe(false);
    expect(shouldAutoplay({ ...base, signedIn: false })).toBe(false);
  });
});
