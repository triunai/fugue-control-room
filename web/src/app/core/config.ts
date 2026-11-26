import { InjectionToken } from '@angular/core';
import { environment } from '../../environments/environment';

export interface ConsoleConfig {
  /** Path prefix of the API. Relative: the dev server proxies /api to the API (proxy.conf.json), so no CORS is needed. */
  apiBase: string;
  /** Absolute origin the API can reach ITSELF on; used to subscribe the Development webhook sink. */
  apiOrigin: string;
  /** Development affordances: demo accounts on sign-in, the webhook dev sink tab. */
  development: boolean;
}

export const CONSOLE_CONFIG = new InjectionToken<ConsoleConfig>('CONSOLE_CONFIG', {
  providedIn: 'root',
  factory: () => ({
    apiBase: '/api/v1',
    apiOrigin: environment.apiOrigin || location.origin,
    development: environment.development,
  }),
});
