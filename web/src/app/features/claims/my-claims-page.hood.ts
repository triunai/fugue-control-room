import { HoodNote } from '../hood/hood-note';

export const MY_CLAIMS_HOOD: HoodNote = {
  why: [
    'The server scopes the list to your own claims; the page sends no owner filter, so there is nothing to tamper with.',
    'A claim that is not yours is a 404, the same answer as one that does not exist.',
  ],
  decisions: [
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-019 Ownership by scoping, 404 across owners', path: 'docs/decisions.md' },
    { label: 'D-056 Claims seam review', path: 'docs/decisions.md' },
  ],
  guards: ['404 outside your scope', '403 permission'],
};
