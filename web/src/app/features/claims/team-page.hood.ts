import { HoodNote } from '../hood/hood-note';

export const TEAM_HOOD: HoodNote = {
  why: [
    'Workload and exposure on one page: the team part needs CLAIMS_WORKLOAD, the money part CLAIMS_EXPOSURE (permission codes from /me).',
    'Money is per currency: each market settles in its own, so there is no grand total, and bars are scaled within one currency.',
  ],
  decisions: [
    { label: 'Workload and exposure', path: 'docs/CLAIMS.md#6-workload-and-exposure' },
    { label: 'Money in minor units', path: 'docs/CLAIMS.md#7-money' },
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
    { label: 'D-044 /me drives permission checks', path: 'docs/decisions.md' },
  ],
  guards: ['403 permission', 'never summed across currencies'],
};
