// Post-step of `npm run api:gen`: the generated LoginRequest has a `password: string;` property, which the repo's
// staged-secrets scan (scripts/spine/secrets-scan.sh) reads as a password assignment. Mark exactly those type
// declarations with the scan's inline ignore marker. Nothing else in the generated code is touched.
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const dir = new URL('../src/app/api/generated/model/', import.meta.url).pathname;
let marked = 0;
for (const file of readdirSync(dir)) {
  const path = join(dir, file);
  const text = readFileSync(path, 'utf8');
  const next = text.replace(/^(\s*password\??: string;)$/gm, (line) => {
    marked++;
    return `${line} // secrets-scan:ignore (a type, not a value)`;
  });
  if (next !== text) writeFileSync(path, next);
}
console.log(`api-gen-post: marked ${marked} password type declaration(s)`);
