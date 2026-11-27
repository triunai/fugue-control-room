import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Calendar } from 'fullcalendar';
import dayGridPlugin from 'fullcalendar/daygrid';
import timeGridPlugin from 'fullcalendar/timegrid';
import classicTheme from 'fullcalendar/themes/classic';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { AccessService } from '../../core/auth/access.service';
import { ErrorState } from '../../ui/states';
import { STATUS_TONE } from './claims-logic';
import { loadStylesheet } from './lazy-css';
import { fetchOpenClaims } from './open-claims';
import { calendarEvents } from './sla-events';

const CALENDAR_CSS = [
  'vendor/fullcalendar/skeleton.css',
  'vendor/fullcalendar/themes/classic/theme.css',
  'vendor/fullcalendar/themes/classic/palette.css',
];

/**
 * The SLA calendar: one event per open claim on its due date (`slaDue`), coloured by status tone. Claimants see their own
 * claims; staff can narrow to the claims assigned to them. FullCalendar is imported only here, so it stays in this lazy
 * route. It is driven through its framework-free `Calendar` class: the Angular wrapper pulls extra Angular core code into
 * the initial bundle (see the web README, "Nice to haves").
 */
@Component({
  selector: 'fg-claims-calendar-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErrorState],
  styleUrl: './claims-calendar.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">SLA calendar</h1>
          <p class="page-lede">
            Each open claim on the day its next step is due: pickup 2 days after it is submitted, a decision 14 days
            after, an answer 7 days after the question, settlement 7 days after approval. Click an event to open the
            claim.
          </p>
        </div>
        @if (isStaff()) {
          <div class="cal-seg" data-tour="sla-filter" role="radiogroup" aria-label="Show">
            <button type="button" role="radio" [attr.aria-checked]="assigned() === 'any'" (click)="assigned.set('any')">
              All open
            </button>
            <button type="button" role="radio" [attr.aria-checked]="assigned() === 'me'" (click)="assigned.set('me')">
              Assigned to me
            </button>
          </div>
        }
      </header>

      @if (query.isError()) {
        <div class="panel"><fg-error-state [error]="query.error()" (retry)="query.refetch()" /></div>
      }
      <div class="panel panel-pad cal-wrap" data-testid="sla-calendar" data-tour="sla-calendar">
        <div class="cal-host"></div>
      </div>
      @if (query.isSuccess()) {
        <p class="text-muted text-sm" data-testid="sla-count">
          {{ events().length }} open claim{{ events().length === 1 ? '' : 's' }} on the calendar.
        </p>
      }
    </div>
  `,
})
export class ClaimsCalendarPage implements AfterViewInit, OnDestroy {
  private readonly api = inject(ClaimsApi);
  private readonly access = inject(AccessService);
  private readonly router = inject(Router);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private calendar: Calendar | null = null;
  private readonly ready = signal(false);

  protected readonly isStaff = computed(() => this.access.has('CLAIMS_ALL'));
  protected readonly assigned = signal<'any' | 'me'>('any');

  protected readonly query = injectQuery(() => ({
    queryKey: [...claimKeys.all, 'calendar', this.assigned()] as const,
    queryFn: () => fetchOpenClaims(this.api, this.assigned()),
  }));

  protected readonly events = computed(() => calendarEvents(this.query.data() ?? [], STATUS_TONE));

  constructor() {
    // Events follow the data once the calendar exists.
    effect(() => {
      const events = this.events();
      if (this.ready()) this.calendar?.setOption('events', events);
    });
  }

  /** FullCalendar's stylesheets are vendor assets loaded with this route; the grid is created once they have. */
  async ngAfterViewInit(): Promise<void> {
    await Promise.all(CALENDAR_CSS.map(loadStylesheet));
    const host = this.element.nativeElement.querySelector<HTMLElement>('.cal-host');
    if (!host || this.calendar) return;
    this.calendar = new Calendar(host, {
      plugins: [classicTheme, dayGridPlugin, timeGridPlugin],
      initialView: 'dayGridMonth',
      headerToolbar: { start: 'prev,next today', center: 'title', end: 'dayGridMonth,timeGridWeek' },
      height: 'auto',
      events: this.events(),
      eventClick: (arg) => void this.router.navigate(['/claims', arg.event.id]),
    });
    this.calendar.render();
    this.ready.set(true);
  }

  ngOnDestroy(): void {
    this.calendar?.destroy();
    this.calendar = null;
  }
}
