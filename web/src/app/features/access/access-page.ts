import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fg-access-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page"><h1 class="page-title">AccessPage</h1></div>`,
})
export class AccessPage {}
