import { HoodNote } from '../hood/hood-note';

export const REPORT_HOOD: HoodNote = {
  why: [
    'One form, one submit: the server validates and answers field errors (400) that land on the fields.',
    'Submit needs an Idempotency-Key. A retry of the same form reuses its key and replays the first reply; an edited form gets a new key.',
  ],
  decisions: [
    { label: 'D-050 Idempotency keys with stored replay', path: 'docs/decisions.md' },
    { label: 'ADR-013 Idempotency keys', path: 'docs/adr/ADR-013-idempotency-keys.md' },
    { label: 'D-056 Submit key is required', path: 'docs/decisions.md' },
    { label: 'Duplicates and atomicity', path: 'docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity' },
  ],
  guards: ['400 idempotency.key_required', '400 validation', '422 same key, different body', '403 permission'],
};
