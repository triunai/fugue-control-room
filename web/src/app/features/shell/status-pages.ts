import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'fg-unavailable-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="page">
      <div class="panel mx-auto mt-10 max-w-xl px-8 py-10 text-center">
        <h1 class="page-title">{{ reason() === 'changed' ? 'This area was just switched off' : 'Not available' }}</h1>
        <p class="page-lede mx-auto">
          @if (reason() === 'changed') {
            Your access changed while you were here: a module was disabled or a permission was withdrawn. Nothing you
            saved is lost.
          } @else {
            This area is not part of your access, or its module is switched off. An administrator can change that on the
            Modules or Users and access pages.
          }
        </p>
        <a routerLink="/" class="btn btn-primary mt-6">Go to your start page</a>
      </div>
    </div>
  `,
})
export class UnavailablePage {
  readonly reason = input<string>();
}

@Component({
  selector: 'fg-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="page">
      <div class="panel mx-auto mt-10 max-w-xl px-8 py-10 text-center">
        <h1 class="page-title">Not found</h1>
        <p class="page-lede mx-auto">
          There is no page at this address. Check the link, or start again from the navigation.
        </p>
        <a routerLink="/" class="btn btn-primary mt-6">Go to your start page</a>
      </div>
    </div>
  `,
})
export class NotFoundPage {}
