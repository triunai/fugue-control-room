import pkg from '../../../package.json';
import { APP_VERSION, footerLabel } from './version';

describe('footerLabel', () => {
  it('shows the package version', () => {
    expect(APP_VERSION).toBe(pkg.version);
    expect(footerLabel('0.0.2')).toBe('Single-tenant \u00b7 v0.0.2 \u00b7 API v1');
    expect(footerLabel()).toContain(`v${pkg.version}`);
  });
});
