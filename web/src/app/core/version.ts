import pkg from '../../../package.json';

/** The console version, read from package.json at build time. */
export const APP_VERSION: string = pkg.version;

/** The sidebar footer line. */
export function footerLabel(version: string = APP_VERSION): string {
  return `Single-tenant \u00b7 v${version} \u00b7 API v1`;
}
