import { TestBed } from '@angular/core/testing';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { RoleDto, RolesApi } from '../../api/roles.api';
import { RolesMatrix } from './roles-matrix';
import { buildMatrix } from './roles-matrix.logic';

const ROLES: RoleDto[] = [
  {
    code: 'STAFF',
    name: 'Staff',
    permissions: [
      { code: 'CLAIMS_READ', module: 'CLAIMS' },
      { code: 'CUSTOMERS_VIEW', module: 'CUSTOMERS' },
    ],
  },
  { code: 'SELF', name: 'Self-service', permissions: [{ code: 'CLAIMS_READ', module: 'CLAIMS' }] },
];

describe('buildMatrix', () => {
  it('groups permissions by module, sorted, with the roles holding each', () => {
    const m = buildMatrix(ROLES);
    expect(m.roles.map((r) => r.code)).toEqual(['STAFF', 'SELF']);
    expect(m.groups.map((g) => g.module)).toEqual(['CLAIMS', 'CUSTOMERS']);
    expect([...m.groups[0].rows[0].granted].sort()).toEqual(['SELF', 'STAFF']);
    expect([...m.groups[1].rows[0].granted]).toEqual(['STAFF']);
  });

  it('is empty for no roles', () => {
    expect(buildMatrix([]).groups).toEqual([]);
  });
});

describe('RolesMatrix', () => {
  it('renders roles as columns, a dot only where a role grants the permission', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideTanStackQuery(() => new QueryClient()),
        { provide: RolesApi, useValue: { list: () => Promise.resolve(ROLES) } },
      ],
    });
    const fixture = TestBed.createComponent(RolesMatrix);
    await fixture.whenStable();
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const heads = [...root.querySelectorAll('thead th')].map((e) => e.textContent?.trim());
    expect(heads).toEqual(['Permission', 'STAFF', 'SELF']);
    const rows = [...root.querySelectorAll('tbody tr:not(.module-row)')];
    const dots = rows.map((r) => [...r.querySelectorAll('td')].map((td) => td.querySelector('.dot') !== null));
    expect(dots).toEqual([
      [true, true], // CLAIMS_READ: both roles
      [true, false], // CUSTOMERS_VIEW: STAFF only
    ]);
    expect(root.textContent).toContain('SELF does not grant CUSTOMERS_VIEW');
  });
});
