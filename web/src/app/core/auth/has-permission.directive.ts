import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AccessService } from './access.service';

/**
 * Structural permission check: `*hasPermission="'CUSTOMERS_ALL'"` or `*hasPermission="['A', 'B']"` (any of).
 * Optional else template: `*hasPermission="'X'; else readOnly"`. Re-evaluates whenever /me changes.
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  private readonly access = inject(AccessService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);

  readonly hasPermission = input.required<string | readonly string[]>();
  readonly hasPermissionElse = input<TemplateRef<unknown> | null>(null);

  private shown: 'then' | 'else' | null = null;

  constructor() {
    effect(() => {
      const allowed = this.access.has(this.hasPermission());
      const next = allowed ? 'then' : this.hasPermissionElse() ? 'else' : null;
      if (next === this.shown) return;
      this.container.clear();
      if (next === 'then') this.container.createEmbeddedView(this.template);
      if (next === 'else') this.container.createEmbeddedView(this.hasPermissionElse()!);
      this.shown = next;
    });
  }
}
