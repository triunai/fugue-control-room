import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import type { RoleDto as GeneratedRoleDto } from './generated/model';

export interface RolePermissionDto {
  code: string;
  module: string;
}

export interface RoleDto {
  code: string;
  name: string;
  permissions: RolePermissionDto[];
}

/** Every role with its permission codes and module, for the role x permission matrix (GET /roles, USERS_VIEW). */
@Injectable({ providedIn: 'root' })
export class RolesApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(CONSOLE_CONFIG).apiBase;

  async list(): Promise<RoleDto[]> {
    const roles = await firstValueFrom(this.http.get<GeneratedRoleDto[]>(`${this.base}/roles`));
    return roles
      .filter((r) => r.isActive)
      .map((r) => ({
        code: r.code,
        name: r.name,
        permissions: r.permissions.map((p) => ({ code: p.code, module: p.module })),
      }));
  }
}
