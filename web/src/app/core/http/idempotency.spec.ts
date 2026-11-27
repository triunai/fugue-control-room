import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CONSOLE_CONFIG } from '../config';
import { CustomersApi } from '../../api/customers.api';
import { WebhooksApi } from '../../api/webhooks.api';
import { SubmissionKey, idempotencyHeaders, newIdempotencyKey } from './idempotency';

describe('SubmissionKey', () => {
  it('reuses the key while the same body is retried', () => {
    const submission = new SubmissionKey();
    const first = submission.forBody({ name: 'Ada', email: 'ada@example.test' });

    expect(submission.forBody({ name: 'Ada', email: 'ada@example.test' })).toBe(first);
  });

  it('gives a changed body a fresh key, because the same key with another body is refused with 422', () => {
    const submission = new SubmissionKey();
    const first = submission.forBody({ name: 'Ada' });

    expect(submission.forBody({ name: 'Ada Lovelace' })).not.toBe(first);
  });

  it('starts clean after a success', () => {
    const submission = new SubmissionKey();
    const first = submission.forBody({ name: 'Ada' });
    submission.reset();

    expect(submission.forBody({ name: 'Ada' })).not.toBe(first);
  });

  it('generates distinct keys', () => {
    expect(newIdempotencyKey()).not.toBe(newIdempotencyKey());
  });
});

describe('idempotencyHeaders', () => {
  it('sends the key as one quoted string and escapes quotes and backslashes', () => {
    expect(idempotencyHeaders('abc')).toEqual({ 'Idempotency-Key': '"abc"' });
    expect(idempotencyHeaders('a"b\\c')).toEqual({ 'Idempotency-Key': '"a\\"b\\\\c"' });
  });
});

describe('create requests', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CONSOLE_CONFIG, useValue: { apiBase: '/api/v1' } },
      ],
    });
    return { ctl: TestBed.inject(HttpTestingController) };
  }

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('POST /customers carries the key it was given', async () => {
    const { ctl } = setup();
    const created = TestBed.inject(CustomersApi).create(
      { name: 'Ada', email: 'ada@example.test', phone: null },
      'key-1',
    );

    const req = ctl.expectOne('/api/v1/customers');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Idempotency-Key')).toBe('"key-1"');
    req.flush({ id: 'c1' });
    await created;
  });

  it('POST /webhooks/subscriptions carries the key it was given', async () => {
    const { ctl } = setup();
    const created = TestBed.inject(WebhooksApi).create(
      { name: 'Hook', url: 'https://example.test/h', eventTypes: ['*'] },
      'key-2',
    );

    const req = ctl.expectOne('/api/v1/webhooks/subscriptions');
    expect(req.request.headers.get('Idempotency-Key')).toBe('"key-2"');
    req.flush({ id: 's1', secret: 'whsec_x' });
    await created;
  });
});
