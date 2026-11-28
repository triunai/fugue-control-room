import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { WebhooksApi } from '../../api/webhooks.api';
import { CorrelationService } from '../../core/http/correlation.service';
import { ToastStore } from '../../core/ui/toast.store';
import { SinkTab } from './sink-tab';

interface Call {
  body: unknown;
  key: string;
}

/** Retrying a submission after a lost response must replay the first result: same body, same Idempotency-Key. */
describe('SinkTab submissions', () => {
  const subscriptionCalls: Call[] = [];
  let failNext = 0;

  function setup() {
    subscriptionCalls.length = 0;
    failNext = 0;
    const maybeFail = () => (failNext-- > 0 ? Promise.reject(new Error('response lost')) : Promise.resolve());
    TestBed.configureTestingModule({
      providers: [
        provideTanStackQuery(() => new QueryClient({ defaultOptions: { queries: { retry: false } } })),
        {
          provide: WebhooksApi,
          useValue: {
            sinkTarget: 'http://sink.test/hook',
            sink: () => Promise.resolve([]),
            subscriptions: () => Promise.resolve({ items: [], nextCursor: null }),
            setSinkSecret: () => Promise.resolve(),
            create: async (body: unknown, key: string) => {
              subscriptionCalls.push({ body, key });
              await maybeFail();
              return { id: 's1', name: 'Development sink', secret: 'whsec_x' };
            },
          },
        },
        { provide: ToastStore, useValue: { ok: () => undefined, problem: () => undefined } },
        { provide: CorrelationService, useValue: { id: () => 'ref-1', begin: () => undefined } },
      ],
    });
    const cmp = TestBed.createComponent(SinkTab).componentInstance as unknown as {
      connect: { mutateAsync(): Promise<string> };
    };
    return cmp;
  }

  it('connect: a retry after a failed attempt reuses the key and the body, a later submission gets a new key', async () => {
    const cmp = setup();
    failNext = 1;

    await expect(cmp.connect.mutateAsync()).rejects.toThrow('response lost');
    await cmp.connect.mutateAsync();
    await cmp.connect.mutateAsync();

    expect(subscriptionCalls).toHaveLength(3);
    expect(subscriptionCalls[1]).toEqual(subscriptionCalls[0]);
    expect(subscriptionCalls[2].key).not.toBe(subscriptionCalls[0].key); // the first success reset the submission
  });

  it('connect subscribes the sink to claim.*, matching the seeded subscription', async () => {
    const cmp = setup();
    await cmp.connect.mutateAsync();
    expect((subscriptionCalls[0].body as { eventTypes: string[] }).eventTypes).toEqual(['claim.*']);
  });

  it('step 2 points at the claims queue and offers no customer action', () => {
    setup();
    const fixture = TestBed.createComponent(SinkTab);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Move a claim');
    expect(text).not.toContain('Create a test customer');
    expect(text).not.toContain('Change a customer');
  });
});
