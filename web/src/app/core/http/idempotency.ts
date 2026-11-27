/** The create endpoints accept an optional `Idempotency-Key` (ADR-013): the same key with the same body returns the first response instead of creating again. */
export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';

/** A fresh key. The API takes it as one quoted string; a UUID needs no escaping. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

/** The header value for a key: a Structured Field String, quoted. */
export function idempotencyHeaders(key: string): Record<string, string> {
  return { [IDEMPOTENCY_KEY_HEADER]: `"${key.replace(/[\\"]/g, '\\$&')}"` };
}

/**
 * One key per form submission. Retrying the SAME body (after a timeout, a 5xx or a refused attempt) reuses the key, so the server answers with the
 * first result instead of creating twice; a changed body is a new submission and gets a fresh key (the same key with another body is 422).
 * Call {@link reset} after a success so the next submission starts clean.
 */
export class SubmissionKey {
  private key: string | null = null;
  private fingerprint: string | null = null;

  forBody(body: unknown): string {
    const fingerprint = JSON.stringify(body);
    if (this.key === null || fingerprint !== this.fingerprint) {
      this.key = newIdempotencyKey();
      this.fingerprint = fingerprint;
    }
    return this.key;
  }

  reset(): void {
    this.key = null;
    this.fingerprint = null;
  }
}
