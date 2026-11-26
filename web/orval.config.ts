import { defineConfig } from 'orval';

/**
 * `npm run api:gen`: Angular HttpClient services + models from the committed OpenAPI snapshot (W-016).
 * The console uses the MODELS (re-exported by src/app/api/models.ts); its thin wrappers in src/app/api/*.api.ts
 * own paging headers, ETag context and query keys, so the generated services are not imported (tree-shaken).
 */
export default defineConfig({
  fugue: {
    input: { target: '../docs/api/openapi.v1.json' },
    output: {
      mode: 'single',
      client: 'angular',
      target: 'src/app/api/generated/fugue.service.ts',
      schemas: 'src/app/api/generated/model',
      clean: true,
      prettier: true,
    },
  },
});
