import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fg-profile-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="page"><h1 class="page-title">ProfilePage</h1></div>`,
})
export class ProfilePage {}
