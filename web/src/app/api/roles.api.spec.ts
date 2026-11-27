import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RolesApi } from './roles.api';

describe('RolesApi', () => {
  it('maps GET /roles to the matrix shape and drops inactive roles', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(RolesApi);
    const ctl = TestBed.inject(HttpTestingController);
    const result = api.list();
    ctl
      .expectOne((r) => r.url.endsWith('/roles'))
      .flush([
        {
          code: 'ADMIN',
          name: 'Administrator',
          isActive: true,
          permissions: [{ code: 'AUDIT_READ', module: 'AUDIT' }],
        },
        { code: 'OLD', name: 'Retired', isActive: false, permissions: [] },
      ]);
    expect(await result).toEqual([
      { code: 'ADMIN', name: 'Administrator', permissions: [{ code: 'AUDIT_READ', module: 'AUDIT' }] },
    ]);
    ctl.verify();
  });
});
