import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { TourService } from './tour.service';

/** First-visit welcome: what Fugue is, how to run it, where Help lives. A native modal dialog (focus trap, Escape). */
@Component({
  selector: 'fg-welcome',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dlg class="welcome" aria-labelledby="welcome-title" data-testid="welcome" (cancel)="close($event, false)">
      <h2 id="welcome-title" class="welcome-title">Welcome to Fugue</h2>
      <p class="welcome-lede">
        A claims console for motor and property claims across six APAC markets, built on .NET 8 and Angular.
      </p>
      <h3 class="section-title">Run it locally</h3>
      <ul class="welcome-list">
        <li><code class="mono">./start.sh</code> or <code class="mono">docker compose up -d --build</code></li>
        <li>Migrations run by themselves (a one-shot migrate service) and the database is seeded.</li>
        <li>
          API on <code class="mono">:5081</code> (<code class="mono">/swagger</code>), this console on
          <code class="mono">:8080</code>.
        </li>
      </ul>
      <p class="welcome-help">
        Stuck on a page? The <strong>Help</strong> button in the top bar explains the page you are on.
      </p>
      <div class="welcome-actions">
        <button type="button" class="btn btn-quiet" data-testid="welcome-skip" (click)="close($event, false)">
          Skip
        </button>
        <button type="button" class="btn btn-primary" data-testid="welcome-take" (click)="close($event, true)">
          Take the tour
        </button>
      </div>
    </dialog>
  `,
  styles: `
    .welcome {
      width: min(34rem, calc(100vw - 2rem));
      padding: 1.5rem 1.6rem;
      color: var(--text);
      background: var(--surface2);
      border: 1px solid var(--line);
      border-radius: var(--radius-overlay);
      box-shadow: var(--shadow-overlay);
    }
    .welcome::backdrop {
      background: var(--scrim);
    }
    .welcome-title {
      margin: 0 0 0.4rem;
      font-family: var(--font-display);
      font-size: 1.9rem;
      font-weight: 600;
    }
    .welcome-lede,
    .welcome-help {
      margin: 0 0 1rem;
      color: var(--muted);
    }
    .welcome-list {
      margin: 0.4rem 0 1rem;
      padding-left: 1.1rem;
      display: grid;
      gap: 0.35rem;
    }
    .welcome-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
    }
  `,
})
export class WelcomeDialog {
  private readonly tour = inject(TourService);
  private readonly dlg = viewChild.required<ElementRef<HTMLDialogElement>>('dlg');

  constructor() {
    effect(() => {
      const el = this.dlg().nativeElement;
      if (this.tour.welcomeOpen()) {
        if (!el.open) el.showModal();
      } else if (el.open) el.close();
    });
  }

  protected close(event: Event, takeTour: boolean): void {
    event.preventDefault();
    this.tour.closeWelcome(takeTour);
  }
}
