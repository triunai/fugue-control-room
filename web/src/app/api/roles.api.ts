import { Injectable } from '@angular/core';

export interface RolePermissionDto {
  code: string;
  module: string;
}

export interface RoleDto {
  code: string;
  name: string;
  permissions: RolePermissionDto[];
}

const p = (module: string, ...codes: string[]): RolePermissionDto[] => codes.map((code) => ({ code, module }));

const CUSTOMERS_STAFF = p(
  'CUSTOMERS',
  'CUSTOMERS_ALL',
  'CUSTOMERS_DELETE',
  'CUSTOMERS_EDIT',
  'CUSTOMERS_INSERT',
  'CUSTOMERS_SUSPEND',
  'CUSTOMERS_VIEW',
);
const CLAIMS_STAFF = p('CLAIMS', 'CLAIMS_ALL', 'CLAIMS_HANDLE', 'CLAIMS_READ', 'CLAIMS_WORKLOAD');

/** The seeded roles exactly as the migration seeds them (checked against the database). PLACEHOLDER data. */
const SEEDED_ROLES: RoleDto[] = [
  {
    code: 'ADMIN',
    name: 'Administrator',
    permissions: [
      ...p('AUDIT', 'AUDIT_READ'),
      ...p(
        'CLAIMS',
        'CLAIMS_ALL',
        'CLAIMS_EXPOSURE',
        'CLAIMS_HANDLE',
        'CLAIMS_READ',
        'CLAIMS_RESPOND',
        'CLAIMS_SETTLE',
        'CLAIMS_SUBMIT',
        'CLAIMS_WORKLOAD',
      ),
      ...CUSTOMERS_STAFF,
      ...p('IDENTITY', 'GROUPS_MANAGE', 'GROUPS_VIEW', 'OVERRIDES_MANAGE', 'OVERRIDES_VIEW'),
      ...p('SYSTEM', 'MODULES_MANAGE', 'MODULES_VIEW'),
      ...p('WEBHOOKS', 'WEBHOOKS_MANAGE'),
    ],
  },
  {
    code: 'MANAGER',
    name: 'Manager',
    permissions: [...CLAIMS_STAFF, ...p('CLAIMS', 'CLAIMS_EXPOSURE', 'CLAIMS_SETTLE'), ...CUSTOMERS_STAFF],
  },
  { code: 'STAFF', name: 'Staff', permissions: [...CLAIMS_STAFF, ...CUSTOMERS_STAFF] },
  {
    code: 'SELF_SERVICE',
    name: 'Self-service',
    permissions: [
      ...p('CLAIMS', 'CLAIMS_READ', 'CLAIMS_RESPOND', 'CLAIMS_SUBMIT'),
      ...p('CUSTOMERS', 'CUSTOMERS_EDIT', 'CUSTOMERS_VIEW'),
    ],
  },
];

/**
 * Every role with its permission codes and module, for the role x permission matrix.
 *
 * PLACEHOLDER: GET /api/v1/roles (lane nth-api, USERS_VIEW) does not exist on this branch yet. After the merge,
 * replace the body of `list()` with `firstValueFrom(this.http.get<RoleDto[]>(`${base}/roles`))` (adjust the
 * mapping to the generated client's shape); nothing else changes.
 */
@Injectable({ providedIn: 'root' })
export class RolesApi {
  list(): Promise<RoleDto[]> {
    return Promise.resolve(SEEDED_ROLES);
  }
}
