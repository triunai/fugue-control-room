import { HoodNote } from '../hood/hood-note';

export const HOME_HOOD: HoodNote = {
  why: [
    'The landing is chosen from permission codes, not role names: admin, manager, officer, claimant. Anyone else goes to their first nav item.',
    'Each persona gets one primary action and reads only what its scope allows.',
  ],
  decisions: [
    { label: 'D-044 /me drives navigation and checks', path: 'docs/decisions.md' },
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
    { label: 'D-053 Claims platform shape', path: 'docs/decisions.md' },
  ],
  guards: ['403 permission', '404 outside your scope'],
};
