import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fg-audit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page"><h1 class="page-title">AuditPage</h1></div>`,
})
export class AuditPage {}
