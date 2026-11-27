import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { AuditApi } from '../../api/audit.api';
import { AccessService } from '../../core/auth/access.service';
import { LastChanged } from './last-changed';

describe('LastChanged', () => {
  function setup(auditOn: boolean, code = 'CUSTOMERS') {
    const list = vi.fn().mockResolvedValue({ items: [] });
    TestBed.configureTestingModule({
      providers: [
        provideTanStackQuery(() => new QueryClient()),
        { provide: AuditApi, useValue: { list } },
        { provide: AccessService, useValue: { has: () => true } },
      ],
    });
    const f = TestBed.createComponent(LastChanged);
    f.componentRef.setInput('code', code);
    f.componentRef.setInput('version', 3);
    f.componentRef.setInput('auditOn', auditOn);
    f.detectChanges();
    return { f, list };
  }

  it('shows a plain fallback and never requests audit while AUDIT is off', async () => {
    const { f, list } = setup(false);
    await f.whenStable();
    f.detectChanges();
    const el = (f.nativeElement as HTMLElement).querySelector('span');
    expect(el?.textContent).toBe('—');
    expect(el?.getAttribute('title')).toBe('audit is off');
    expect(list).not.toHaveBeenCalled();
  });

  it('does not spin forever for a module without an audit id', async () => {
    const { f } = setup(true, 'UNSEEDED');
    await f.whenStable();
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toBe('version 3');
  });

  it('requests audit when AUDIT is on', async () => {
    const { f, list } = setup(true);
    await f.whenStable();
    expect(list).toHaveBeenCalled();
  });
});
