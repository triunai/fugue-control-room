import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

describe('record banner step track', () => {
  it('ends the connector line at the last node instead of the card edge', () => {
    const css = readFileSync(resolve(__dirname, 'claim-record.css'), 'utf8');
    expect(css).toMatch(/\.step:last-child::before\s*\{[^}]*right:\s*auto;[^}]*width:\s*1\.15rem/);
  });
});
