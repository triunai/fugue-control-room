import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fg-webhooks-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page"><h1 class="page-title">WebhooksPage</h1></div>`,
})
export class WebhooksPage {}
