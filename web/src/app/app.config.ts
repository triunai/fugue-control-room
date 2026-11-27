import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
  withNavigationErrorHandler,
} from '@angular/router';
import { reloadOnStaleChunk } from './core/stale-chunk';
import { provideTanStackQuery } from '@tanstack/angular-query';
import { routes } from './app.routes';
import { consoleInterceptors } from './core/http/interceptors';
import { hoodOffFromUrl } from './features/hood/hood-state';
import { suppressTourFromUrl } from './features/shell/tour-steps';
import { createQueryClient } from './core/query/query-client';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAppInitializer(suppressTourFromUrl),
    provideAppInitializer(hoodOffFromUrl),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
      withNavigationErrorHandler(reloadOnStaleChunk),
    ),
    provideHttpClient(withFetch(), withInterceptors(consoleInterceptors)),
    provideTanStackQuery(createQueryClient),
  ],
};
