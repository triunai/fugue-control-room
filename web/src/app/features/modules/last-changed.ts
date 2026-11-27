import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { injectQuery } from '@tanstack/angular-query';
import { AuditApi, EMPTY_AUDIT_FILTER } from '../../api/audit.api';
import { AccessService } from '../../core/auth/access.service';
import { MODULE_ENTITY_IDS } from './when-off';

const when = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

/** "Dev Admin · 27 Nov 2026, 16:39" from the newest audit row for the module; needs AUDIT_READ. */
@Component({
  selector: 'fg-last-changed',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="text-sm" [attr.title]="auditOn() ? null : 'audit is off'">{{ text() }}</span>`,
})
export class LastChanged {
  private readonly audit = inject(AuditApi);
  private readonly access = inject(AccessService);

  readonly code = input.required<string>();
  readonly version = input.required<number>();
  /** False while the AUDIT module is switched off: its endpoint answers 404, so nothing is requested. */
  readonly auditOn = input(true);

  protected readonly latest = injectQuery(() => ({
    queryKey: ['modules', this.code(), 'last-changed', this.auditOn()],
    enabled: this.auditOn() && this.access.has('AUDIT_READ') && !!MODULE_ENTITY_IDS[this.code()],
    retry: false,
    queryFn: async () =>
      (
        await this.audit.list(
          { ...EMPTY_AUDIT_FILTER, entityType: 'Module', entityId: MODULE_ENTITY_IDS[this.code()] },
          null,
          1,
        )
      ).items[0] ?? null,
  }));

  protected readonly text = computed(() => {
    if (!this.auditOn()) return '—';
    const row = this.latest.data();
    if (row) return `${row.actorDisplay ?? row.actorType} · ${when.format(new Date(row.occurredAt))}`;
    if (!this.access.has('AUDIT_READ') || this.latest.isError()) return `version ${this.version()}`;
    // A disabled query is "pending" forever: only spin while a request is really in flight.
    if (!MODULE_ENTITY_IDS[this.code()]) return `version ${this.version()}`;
    return this.latest.isPending() ? '…' : 'default (on)';
  });
}
