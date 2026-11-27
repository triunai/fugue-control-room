import { ClaimDto } from '../../api/models';

export type LandingKind = 'claimant' | 'officer' | 'manager' | 'admin' | 'other';

/**
 * Which landing a signed-in person gets, from permission CODES (never role names). The broadest persona wins:
 * module management is the admin, claims workload plus exposure the manager, handling claims the officer, and
 * submitting claims without seeing everyone's the claimant. Anyone else ('other') is sent to their first nav item.
 */
export function landingKind(has: (code: string) => boolean): LandingKind {
  if (has('MODULES_MANAGE')) return 'admin';
  if (has('CLAIMS_WORKLOAD') && has('CLAIMS_EXPOSURE')) return 'manager';
  if (has('CLAIMS_HANDLE')) return 'officer';
  if (has('CLAIMS_SUBMIT') && !has('CLAIMS_ALL')) return 'claimant';
  return 'other';
}

type Mine = Pick<ClaimDto, 'id' | 'status' | 'updatedAt' | 'submittedAt'>;

/** The claimant's hero: the claim that needs them (an open question) first, else the most recently updated open claim. */
export function claimantHero<T extends Mine>(claims: readonly T[]): { claim: T | null; needsYou: boolean } {
  const needs = claims.find((c) => c.status === 'InfoRequested');
  if (needs) return { claim: needs, needsYou: true };
  const open = [...claims]
    .filter((c) => c.status !== 'Settled' && c.status !== 'Rejected')
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))[0];
  return { claim: open ?? null, needsYou: false };
}

/** Oldest submission first: the claim closest to breaching its SLA leads. */
export function bySlaRisk<T extends Pick<ClaimDto, 'submittedAt'>>(claims: readonly T[]): T[] {
  return [...claims].sort((a, b) => a.submittedAt.localeCompare(b.submittedAt));
}

/** The officer's one obvious next click: their riskiest claim that needs action, else the oldest one to pick up. */
export function officerPrimary<T extends Pick<ClaimDto, 'id' | 'number' | 'submittedAt'>>(
  mine: readonly T[],
  unassigned: readonly T[],
): { kind: 'work' | 'pickup'; claim: T } | null {
  const m = bySlaRisk(mine)[0];
  if (m) return { kind: 'work', claim: m };
  const u = bySlaRisk(unassigned)[0];
  return u ? { kind: 'pickup', claim: u } : null;
}
