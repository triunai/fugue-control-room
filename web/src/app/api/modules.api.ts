import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CONSOLE_CONFIG } from '../core/config';
import { EtagStore } from '../core/http/etag.store';
import { ModuleDto } from './models';

export const moduleKeys = { all: ['modules'] as const };

@Injectable({ providedIn: 'root' })
export class ModulesApi {
  private readonly http = inject(HttpClient);
  private readonly etags = inject(EtagStore);
  private readonly base = `${inject(CONSOLE_CONFIG).apiBase}/modules`;

  async list(): Promise<ModuleDto[]> {
    const modules = await firstValueFrom(this.http.get<ModuleDto[]>(this.base));
    // The list carries each module's version (its ETag); the PATCH sends it back as If-Match.
    for (const m of modules) this.etags.seed(`${this.base}/${m.code}`, m.version);
    return modules;
  }

  async setActive(code: string, isActive: boolean): Promise<void> {
    await firstValueFrom(this.http.patch(`${this.base}/${code}`, { isActive }));
  }
}
