import { RoleDto } from '../../api/roles.api';

export interface MatrixRow {
  code: string;
  /** Role codes holding this permission. */
  granted: ReadonlySet<string>;
}

export interface MatrixGroup {
  module: string;
  rows: MatrixRow[];
}

export interface Matrix {
  roles: { code: string; name: string }[];
  groups: MatrixGroup[];
}

/** Roles as columns, permissions grouped by module as rows (modules and codes sorted), a role marked where it grants. */
export function buildMatrix(roles: RoleDto[]): Matrix {
  const byModule = new Map<string, Map<string, Set<string>>>();
  for (const role of roles) {
    for (const perm of role.permissions) {
      const codes = byModule.get(perm.module) ?? new Map<string, Set<string>>();
      const holders = codes.get(perm.code) ?? new Set<string>();
      holders.add(role.code);
      codes.set(perm.code, holders);
      byModule.set(perm.module, codes);
    }
  }
  const groups = [...byModule]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([module, codes]) => ({
      module,
      rows: [...codes].sort(([a], [b]) => a.localeCompare(b)).map(([code, granted]) => ({ code, granted })),
    }));
  return { roles: roles.map((r) => ({ code: r.code, name: r.name })), groups };
}
