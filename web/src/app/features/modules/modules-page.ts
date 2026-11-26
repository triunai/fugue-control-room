import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fg-modules-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page"><h1 class="page-title">ModulesPage</h1></div>`,
})
export class ModulesPage {}
