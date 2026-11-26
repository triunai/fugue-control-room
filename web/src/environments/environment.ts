import { DemoAccount } from './environment.types';

/** Production build: no demo accounts, no Development affordances. */
export const environment = {
  development: false,
  apiOrigin: '',
  demoAccounts: [] as DemoAccount[],
  demoPassword: '',
};
