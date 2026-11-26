import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastRegion } from './ui/toast-region';

@Component({
  selector: 'fg-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ToastRegion],
  template: `<router-outlet /><fg-toast-region />`,
})
export class App {}
