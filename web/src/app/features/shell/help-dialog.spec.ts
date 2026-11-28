import { DialogRef } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccessService } from '../../core/auth/access.service';
import { HelpDialog } from './help-dialog';
import { TourService } from './tour.service';

function render(granted: string[]) {
  const close = vi.fn();
  const start = vi.fn(() => Promise.resolve());
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      { provide: DialogRef, useValue: { close } },
      { provide: TourService, useValue: { start } },
      {
        provide: AccessService,
        useValue: {
          has: (c: string | readonly string[]) => (typeof c === 'string' ? granted.includes(c) : c.some((x) => granted.includes(x))),
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(HelpDialog);
  fixture.detectChanges();
  return { root: fixture.nativeElement as HTMLElement, close, start };
}

describe('HelpDialog', () => {
  it('renders every module and marks only the signed-in user\'s own as "Your role"', () => {
    const { root } = render(['CLAIMS_SUBMIT']);
    for (const id of ['claimant', 'officer', 'manager', 'admin', 'extras']) {
      expect(root.querySelector(`[data-testid="help-module-${id}"]`)).not.toBeNull();
    }
    const own = (id: string) => root.querySelector(`[data-testid="help-module-${id}"] [data-testid="help-own"]`);
    expect(own('claimant')).not.toBeNull();
    expect(own('extras')).not.toBeNull();
    expect(own('officer')).toBeNull();
    expect(root.querySelector('[data-testid="help-module-officer"] [data-testid="help-needs"]')?.textContent).toContain(
      'needs Claims officer',
    );
    expect(root.querySelector('[data-testid="help-module-admin"]')?.classList).toContain('dim');
  });

  it('marks the admin module for a user with admin permissions', () => {
    const { root } = render(['MODULES_VIEW', 'AUDIT_READ']);
    expect(root.querySelector('[data-testid="help-module-admin"] [data-testid="help-own"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="help-module-claimant"] [data-testid="help-own"]')).toBeNull();
  });

  it('links an item the user may open and closes the dialog on navigate', () => {
    const { root, close } = render(['CLAIMS_SUBMIT']);
    const link = root.querySelector<HTMLAnchorElement>('a[href="/claims/new"]');
    expect(link).not.toBeNull();
    link!.click();
    expect(close).toHaveBeenCalled();
    expect(root.querySelector('a[href="/audit"]')).toBeNull();
  });

  it('"Tour this page" closes the dialog and starts the tour', () => {
    const { root, close, start } = render([]);
    root.querySelector<HTMLButtonElement>('[data-testid="help-tour"]')!.click();
    expect(close).toHaveBeenCalled();
    expect(start).toHaveBeenCalledTimes(1);
  });
});
