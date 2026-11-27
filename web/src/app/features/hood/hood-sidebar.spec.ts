import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ApiCallLog } from '../../core/http/api-call-log';
import { HoodSidebar } from './hood-sidebar';
import { HoodNote, noteFor } from './hood-note';
import { HoodState, hoodOffFromUrl, isTypingTarget } from './hood-state';

const NOTE: HoodNote = {
  why: ['First line.', 'Second line.'],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Claims/ClaimsWorkflowTests.cs',
      name: 'A_second_officer_with_the_old_etag_gets_412_and_with_a_fresh_one_gets_409',
      asserts: 'A stale If-Match is 412.',
    },
  ],
  adrs: [{ label: 'ADR-006 Data access', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' }],
  decisions: [{ label: 'D-056 Review', path: 'docs/decisions.md' }],
  how: ['Calls GET /claims/{id}.', 'If-Match is checked first.'],
  guards: [
    {
      status: '412',
      name: 'stale',
      path: 'src/Fugue.Application/Features/Claims/ClaimFlow.cs',
      code: 'if (claim.RowVersion != expected)\n{\n    …\n}',
      note: 'Reload, then retry.',
    },
  ],
};

@Component({ template: '' })
class Blank {}

const CALL = {
  method: 'GET',
  path: '/claims/{id}',
  status: 200,
  ms: 12,
  ifMatch: false,
  etag: true,
  idempotencyKey: false,
  code: null,
};

function render() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'a', component: Blank, data: { hood: NOTE } },
        { path: 'b', component: Blank },
      ]),
    ],
  });
  const fixture = TestBed.createComponent(HoodSidebar);
  fixture.detectChanges();
  return fixture;
}

const q = (f: { nativeElement: HTMLElement }, sel: string) => f.nativeElement.querySelector(sel) as HTMLElement | null;

describe('Under the hood sidebar', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('is open on a first visit and shows the live calls first', () => {
    const f = render();
    TestBed.inject(ApiCallLog).record(CALL);
    f.detectChanges();
    expect(q(f, '[data-testid=hood-calls]')?.textContent).toContain('/claims/{id}');
    const heads = [...f.nativeElement.querySelectorAll('h3')].map((h) => (h as HTMLElement).textContent?.trim());
    expect(heads[0]).toMatch(/^API calls/);
  });

  it('minimises to one tab, remembers the choice, and the tab opens it again', () => {
    const f = render();
    q(f, '[data-testid=hood-minimise]')!.click();
    f.detectChanges();
    expect(q(f, '[data-testid=hood-tab]')).not.toBeNull();
    expect(q(f, '[data-testid=hood-calls]')).toBeNull();
    expect(f.nativeElement.classList.contains('min')).toBe(true);
    expect(localStorage.getItem('fugue.hood.open')).toBe('0');
    q(f, '[data-testid=hood-tab]')!.click();
    f.detectChanges();
    expect(q(f, '[data-testid=hood-minimise]')).not.toBeNull();
    expect(localStorage.getItem('fugue.hood.open')).toBe('1');
  });

  it('starts minimised from a remembered choice, and with ?hood=off without remembering that', () => {
    localStorage.setItem('fugue.hood.open', '0');
    expect(TestBed.inject(HoodState).open()).toBe(false);

    TestBed.resetTestingModule();
    localStorage.clear();
    window.history.replaceState({}, '', '/?hood=off');
    hoodOffFromUrl();
    window.history.replaceState({}, '', '/');
    expect(TestBed.inject(HoodState).open()).toBe(false);
    expect(localStorage.getItem('fugue.hood.open')).toBeNull();
  });

  it('toggles on "." but not while typing and not with a modifier', () => {
    const f = render();
    const state = TestBed.inject(HoodState);
    const input = document.createElement('input');
    document.body.appendChild(input);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: '.', bubbles: true }));
    expect(state.open()).toBe(true);
    expect(isTypingTarget(input)).toBe(true);
    input.remove();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: '.', ctrlKey: true, bubbles: true }));
    expect(state.open()).toBe(true);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: '.', bubbles: true }));
    f.detectChanges();
    expect(state.open()).toBe(false);
  });

  it('shows the note of the active route, and only the calls where there is none', async () => {
    const f = render();
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/a');
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('First line.');
    expect(f.nativeElement.textContent).toContain('Calls GET /claims/{id}.');
    const heads = [...f.nativeElement.querySelectorAll('h3')].map((h) => (h as HTMLElement).textContent?.trim());
    expect(heads).toEqual(['API calls live', 'Why this page', 'How the API does it', 'Guards', 'Tested by', 'Decisions']);
    expect(q(f, '[data-testid=hood-tests] .layer')?.textContent).toBe('integration');
    expect(q(f, '[data-testid=hood-tests] a')?.getAttribute('href')).toContain('tests/Fugue.Tests/Claims/ClaimsWorkflowTests.cs');
    const guard = q(f, 'details.guard')!;
    expect(guard.querySelector('summary')?.textContent?.replace(/\s+/g, ' ').trim()).toBe('412 stale');
    expect(guard.hasAttribute('open')).toBe(false);
    expect(guard.querySelector('pre code')?.textContent).toContain('if (claim.RowVersion != expected)');
    expect(guard.querySelector('a.gpath')?.getAttribute('href')).toBe(
      'https://github.com/triunai/fugue-control-room/blob/main/src/Fugue.Application/Features/Claims/ClaimFlow.cs',
    );
    const links = [...f.nativeElement.querySelectorAll('.links li')] as HTMLElement[];
    expect(links[0].classList.contains('adr')).toBe(true);
    expect(links[1].classList.contains('adr')).toBe(false);
    expect(links[0].querySelector('a')?.getAttribute('href')).toContain('docs/adr/ADR-006');
    expect(links[1].querySelector('a')?.getAttribute('href')).toBe(
      'https://github.com/triunai/fugue-control-room/blob/main/docs/decisions.md',
    );
    await router.navigateByUrl('/b');
    f.detectChanges();
    expect(f.nativeElement.textContent).not.toContain('First line.');
    expect(q(f, '[data-testid=hood]')).not.toBeNull();
  });

  it('clears the call log when the path changes, not when only the query does', async () => {
    render();
    const log = TestBed.inject(ApiCallLog);
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/a');
    log.record(CALL);
    await router.navigateByUrl('/a?status=Open');
    expect(log.count()).toBe(1);
    await router.navigateByUrl('/b');
    expect(log.count()).toBe(0);
  });

  it('noteFor reads data.hood of the deepest route that has one', () => {
    const leaf = { data: { hood: NOTE }, firstChild: null };
    const root = { data: {}, firstChild: { data: {}, firstChild: leaf } };
    expect(noteFor(root as never)).toBe(NOTE);
    expect(noteFor({ data: {}, firstChild: null } as never)).toBeNull();
  });
});
