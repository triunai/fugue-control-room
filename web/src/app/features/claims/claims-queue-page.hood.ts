import { HoodNote } from '../hood/hood-note';

export const CLAIMS_QUEUE_HOOD: HoodNote = {
  why: [
    'Open claims, longest-waiting first, in keyset pages ("Load more"). Tab counts come from one grouped counts call that follows the same filters.',
    'It refreshes on focus and every 15 s. Reserves show in each claim’s own currency and are never added across currencies.',
  ],
  decisions: [
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-053 Claims platform shape', path: 'docs/decisions.md' },
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
  ],
  guards: ['403 permission', '404 outside your scope'],
};
