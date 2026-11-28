import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AccessService } from '../../core/auth/access.service';
import { GUIDE, GuideItem, GuideModule } from './help-guide';
import { tourKeyFor } from './tour-steps';
import { TourService } from './tour.service';

/** The Help dialog: a guide to the app by module, the signed-in user's own modules marked, plus the page tour. */
@Component({
  selector: 'fg-help-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="help" data-testid="help-dialog" role="group" aria-labelledby="help-title">
      <h2 id="help-title" class="help-title">Help: what is where</h2>
      <p class="help-lead">Fugue is split into modules. Yours are marked; the others are dimmed and say which role opens them.</p>
      @for (m of modules; track m.id) {
        <section class="mod" [class.dim]="!mine(m)" [attr.data-testid]="'help-module-' + m.id">
          <h3 class="mod-title">
            {{ m.title }}
            @if (mine(m)) {
              <span class="chip chip-own" data-testid="help-own">Your role</span>
            } @else {
              <span class="chip" data-testid="help-needs">needs {{ m.role }}</span>
            }
          </h3>
          <ul>
            @for (i of m.items; track i.label) {
              <li [class.dim]="!allowed(i)">
                @if (i.path && allowed(i)) {
                  @if (i.external) {
                    <a [href]="i.path" target="_blank" rel="noopener" (click)="ref.close()">{{ i.label }}</a>
                  } @else {
                    <a [routerLink]="i.path" (click)="ref.close()">{{ i.label }}</a>
                  }
                } @else {
                  <strong>{{ i.label }}</strong>
                }
                <span class="txt">{{ i.text }}</span>
              </li>
            }
          </ul>
        </section>
      }
      <div class="help-actions">
        <button type="button" class="btn" data-testid="help-close" (click)="ref.close()">Close</button>
        <button type="button" class="btn btn-primary" data-testid="help-tour" (click)="tourPage()">Tour this page</button>
      </div>
    </div>
  `,
  styles: `
    .help {
      padding: 1.4rem 1.6rem;
      max-height: calc(100vh - 3rem);
      overflow-y: auto;
    }
    .help-title {
      margin: 0 0 0.3rem;
      font-family: var(--font-display);
      font-size: 1.8rem;
      font-weight: 600;
    }
    .help-lead {
      margin: 0 0 0.8rem;
      color: var(--muted);
      font-size: 0.9rem;
    }
    .mod {
      margin: 0.9rem 0;
    }
    .mod-title {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      margin: 0 0 0.3rem;
      font-size: 1rem;
      font-weight: 600;
    }
    .chip {
      font-size: 0.72rem;
      font-weight: 500;
      color: var(--muted);
      border: 1px solid var(--border, currentColor);
      border-radius: 999px;
      padding: 0.05rem 0.55rem;
    }
    .chip-own {
      color: var(--on-accent);
      background: var(--accent);
      border-color: var(--accent);
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li {
      padding: 0.2rem 0;
      font-size: 0.9rem;
    }
    li a {
      font-weight: 600;
    }
    .txt {
      margin-left: 0.5rem;
      color: var(--muted);
    }
    .dim {
      opacity: 0.6;
    }
    .help-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.6rem;
      margin-top: 1rem;
    }
  `,
})
export class HelpDialog {
  protected readonly ref = inject<DialogRef<void>>(DialogRef);
  private readonly access = inject(AccessService);
  private readonly tour = inject(TourService);
  private readonly router = inject(Router);
  protected readonly modules = GUIDE;

  /** The module is the user's own when they hold its gate permission (the nav's own check). Extras are everyone's. */
  protected mine(m: GuideModule): boolean {
    return m.gate.length === 0 || this.access.has(m.gate);
  }
  protected allowed(i: GuideItem): boolean {
    return !i.permission || this.access.has(i.permission);
  }

  protected tourPage(): void {
    const key = tourKeyFor(this.router.url);
    this.ref.close();
    void this.tour.start(key);
  }
}
