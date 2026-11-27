import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type {
  ClaimDto,
  CreateImportedClaimRequest,
  ImportCheckDto,
  ImportDraftInput,
  ImportedClaimDto,
  ImportFileDto,
  ImportSource,
} from '../../api/generated/model';
import { CONSOLE_CONFIG } from '../../core/config';

export const importKeys = {
  recent: ['import', 'recent'] as const,
};

/** The four import endpoints (item 11a). Staff only; the server reads files, checks drafts and creates claims. */
@Injectable({ providedIn: 'root' })
export class ImportApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(CONSOLE_CONFIG).apiBase}/import`;

  readFile(source: ImportSource, file: File): Promise<ImportFileDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(this.http.post<ImportFileDto>(`${this.base}/files`, form, { params: { source } }));
  }

  check(drafts: ImportDraftInput[]): Promise<ImportCheckDto[]> {
    return firstValueFrom(this.http.post<ImportCheckDto[]>(`${this.base}/check`, { drafts }));
  }

  /** No Idempotency-Key: the server's import ledger refuses a second create of the same item (409 naming the claim). */
  create(body: CreateImportedClaimRequest): Promise<ClaimDto> {
    return firstValueFrom(this.http.post<ClaimDto>(`${this.base}/claims`, body));
  }

  recent(limit = 20): Promise<ImportedClaimDto[]> {
    return firstValueFrom(this.http.get<ImportedClaimDto[]>(`${this.base}/claims`, { params: { limit } }));
  }
}
