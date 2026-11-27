import { HoodNote } from '../hood/hood-note';

export const CLAIM_DETAIL_HOOD: HoodNote = {
  why: [
    'Opening the claim reads it and keeps its ETag; every action is sent with that ETag as If-Match, and the reply is the claim as it now stands.',
    'It refreshes on focus and every 15 s (paused mid-form). A stale action shows a Reload banner, never a silent overwrite; a claimant never sees the reserve.',
  ],
  decisions: [
    { label: 'D-056 ETag checked before the state machine', path: 'docs/decisions.md' },
    { label: 'Concurrency and atomicity', path: 'docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity' },
    { label: 'The claim and its state machine', path: 'docs/CLAIMS.md#2-the-claim-and-its-state-machine' },
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
  ],
  guards: [
    '428 If-Match required',
    '412 stale',
    '409 invalid transition',
    '409 not your claim',
    '404 outside your scope',
    '403 permission',
  ],
};
