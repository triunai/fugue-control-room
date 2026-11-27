import { MARKETS, parseMoney } from '../features/claims/claims-logic';
import { EVENT_TYPES } from '../api/webhooks.api';
import {
  ANSWER_EXAMPLES,
  APPROVE_EXAMPLES,
  customerExamples,
  daysAgo,
  INCIDENT_EXAMPLES,
  incidentExamplesFor,
  REJECT_EXAMPLES,
  REQUEST_INFO_EXAMPLES,
  reserveExamples,
  samplePassword,
  samplePolicy,
  userExamples,
  WEBHOOK_EXAMPLES,
} from './examples';

/** The API's E.164 rule for customer phones (CustomerRules.cs). */
const E164 = /^\+[1-9]\d{1,14}$/;

describe('examples (made-up data that must still pass every form rule)', () => {
  it('every market the app has, times Motor and Property, has at least three incidents', () => {
    for (const m of MARKETS)
      for (const line of ['Motor', 'Property'])
        expect(INCIDENT_EXAMPLES.filter((e) => e.market === m.code && e.line === line).length).toBeGreaterThanOrEqual(
          3,
        );
    expect(INCIDENT_EXAMPLES.every((e) => MARKETS.some((m) => m.code === e.market))).toBe(true);
  });

  it('incidents fit the report form and the API: lengths, a past date inside a year, a sample policy', () => {
    for (const e of INCIDENT_EXAMPLES) {
      expect(e.description.length).toBeGreaterThan(20);
      expect(e.description.length).toBeLessThanOrEqual(4000);
      expect(e.location.length).toBeLessThanOrEqual(200);
      expect(e.daysAgo).toBeGreaterThanOrEqual(1);
      expect(e.daysAgo).toBeLessThan(365);
      expect(samplePolicy(e)).toMatch(/^SAMPLE-[A-Z]{2}-(MTR|PRP)-\d+$/);
    }
    expect(new Set(INCIDENT_EXAMPLES.map(samplePolicy)).size).toBe(INCIDENT_EXAMPLES.length);
    expect(daysAgo(2, new Date(2026, 2, 1))).toBe('2026-02-27');
  });

  it('the list narrows to the chosen market and line, and offers one per market when nothing is chosen', () => {
    const none = incidentExamplesFor('', '');
    expect(none.map((e) => e.market)).toEqual(MARKETS.map((m) => m.code));
    expect(incidentExamplesFor('TH', '').every((e) => e.market === 'TH')).toBe(true);
    const both = incidentExamplesFor('MY', 'Property');
    expect(both.length).toBe(3);
    expect(both.every((e) => e.market === 'MY' && e.line === 'Property')).toBe(true);
  });

  it('reserve amounts parse as positive money in every currency', () => {
    for (const m of MARKETS)
      for (const r of reserveExamples(m.currency)) expect(parseMoney(r.amount)).toBeGreaterThan(0);
  });

  it('officer and claimant texts fit the 2000 and 4000 character limits', () => {
    for (const t of [...REQUEST_INFO_EXAMPLES, ...APPROVE_EXAMPLES, ...REJECT_EXAMPLES])
      expect(t.text.length).toBeLessThanOrEqual(2000);
    for (const t of ANSWER_EXAMPLES) expect(t.text.length).toBeLessThanOrEqual(4000);
  });

  it('webhook receivers are public https on documentation domains, with events the dialog offers', () => {
    const choices = ['customer.*', '*', ...EVENT_TYPES];
    for (const w of WEBHOOK_EXAMPLES) {
      const url = new URL(w.url);
      expect(url.protocol).toBe('https:');
      expect(['example.com', 'example.org', 'example.net']).toContain(url.hostname);
      expect(w.events.length).toBeGreaterThan(0);
      expect(w.events.every((e) => choices.includes(e))).toBe(true);
    }
  });

  it('customers and users get example.com emails with a fresh tag, E.164 phones, and a role each', () => {
    const a = customerExamples();
    for (const c of a) {
      expect(c.email).toMatch(/^[a-z.]+\+\d{4}@example\.com$/);
      if (c.phone) expect(c.phone).toMatch(E164);
    }
    for (const u of userExamples()) {
      expect(u.email).toMatch(/@example\.com$/);
      expect(u.roles.length).toBeGreaterThan(0);
    }
  });

  it('sample passwords are random and at least the 12 characters the API asks for', () => {
    const a = samplePassword();
    expect(a.length).toBeGreaterThanOrEqual(12);
    expect(new Set(Array.from({ length: 8 }, samplePassword)).size).toBeGreaterThan(1);
  });
});
