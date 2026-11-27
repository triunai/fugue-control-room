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
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Calendar } from 'fullcalendar';
import dayGridPlugin from 'fullcalendar/daygrid';
import timeGridPlugin from 'fullcalendar/timegrid';
import interactionPlugin from 'fullcalendar/interaction';
import classicTheme from 'fullcalendar/themes/classic';
import { injectQuery } from '@tanstack/angular-query';
import { ClaimsApi, claimKeys } from '../../api/claims.api';
import { AccessService } from '../../core/auth/access.service';
import { DatePicker } from '../../ui/date-picker';
import { ErrorState } from '../../ui/states';
import { STATUS_TONE } from './claims-logic';
import { loadStylesheet } from './lazy-css';
import { fetchOpenClaims } from './open-claims';
import { DueItem, calendarEvents, claimsDueOn, dayKey, shiftDay, weekOf } from './sla-events';
import { ClaimStatusPill } from './claim-bits';

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
  imports: [DatePicker, FormsModule, ErrorState, RouterLink, ClaimStatusPill],
  host: { '(document:keydown.escape)': 'closeDay()' },
  styleUrl: './claims-calendar.css',
  template: `
    <div class="page">
      <header class="page-head">
        <div>
          <h1 class="page-title">SLA calendar</h1>
          <p class="page-lede">
            Each open claim on the day its next step is due: pickup 2 days after it is submitted, a decision 14 days
            after, an answer 7 days after the question, settlement 7 days after approval. Click a day to see what is due
            then (a drawer opens on the right; step through the week from there), or an event to open the claim.
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
      <div class="cal-pick">
        <label class="text-sm" for="cal-day">Show what is due on</label>
        <fg-date inputId="cal-day" [ngModel]="day()" (ngModelChange)="pickDay($event)" />
      </div>
      @if (day(); as d) {
        <div class="dd-scrim" (click)="closeDay()" aria-hidden="true"></div>
        <aside
          class="dd"
          role="dialog"
          aria-modal="false"
          [attr.aria-label]="'Due on ' + dayLabel()"
          data-testid="day-panel"
          tabindex="-1"
        >
          <header class="dd-head">
            <div>
              <p class="dd-eyebrow">Due on</p>
              <h2 class="dd-title">{{ dayLabel() }}</h2>
            </div>
            <button type="button" class="btn btn-sm" (click)="closeDay()">Close</button>
          </header>
          <nav class="dd-week" aria-label="Days of this week">
            <button type="button" class="dd-step" aria-label="Previous week" (click)="pickDay(shift(d, -7))">
              &lsaquo;
            </button>
            @for (w of week(); track w.key) {
              <button
                type="button"
                class="dd-day"
                [class.on]="w.key === d"
                [attr.aria-pressed]="w.key === d"
                [attr.aria-label]="w.label"
                [attr.data-testid]="'dd-day-' + w.key"
                (click)="pickDay(w.key)"
              >
                <span class="dd-dow">{{ w.dow }}</span>
                <span class="dd-num">{{ w.num }}</span>
                <span class="dd-count" [class.zero]="w.count === 0">{{ w.count }}</span>
              </button>
            }
            <button type="button" class="dd-step" aria-label="Next week" (click)="pickDay(shift(d, 7))">
              &rsaquo;
            </button>
          </nav>
          @if (dayCards().length === 0) {
            <p class="text-muted dd-empty" data-testid="day-empty">Nothing due</p>
          } @else {
            <p class="text-muted text-sm dd-sum">
              {{ dayCards().length }} claim{{ dayCards().length === 1 ? '' : 's' }} due{{
                overdueCount() ? ', ' + overdueCount() + ' overdue' : ''
              }}
            </p>
            <ul class="dd-list" data-testid="day-list">
              @for (i of dayCards(); track i.id) {
                <li>
                  <a class="dd-card" [class.late]="i.overdue" [routerLink]="['/claims', i.id]">
                    <span class="dd-row">
                      <span class="mono dd-no">{{ i.number }}</span>
                      <fg-claim-status [status]="i.status" />
                    </span>
                    <span class="dd-what">
                      {{ i.what }}
                      @if (i.overdue) {
                        <span class="dd-late">overdue</span>
                      }
                    </span>
                    <span class="dd-meta">{{ i.market }} &middot; {{ i.line }} &middot; {{ i.claimant }}</span>
                    @if (i.description) {
                      <span class="dd-desc">{{ i.description }}</span>
                    }
                    <span class="dd-meta">Officer: {{ i.officer }}</span>
                    <span class="dd-open">Open claim &rarr;</span>
                  </a>
                </li>
              }
            </ul>
          }
        </aside>
      }
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
  private readonly cssLoaded = signal(false);
  private readonly ready = signal(false);

  protected readonly isStaff = computed(() => this.access.has('CLAIMS_ALL'));
  protected readonly assigned = signal<'any' | 'me'>('any');
  /** The day whose claims are listed under the calendar (local `YYYY-MM-DD`), or '' when none is picked. */
  protected readonly day = signal('');
  protected readonly dayItems = computed<DueItem[]>(() =>
    this.day() ? claimsDueOn(this.query.data() ?? [], this.day()) : [],
  );
  protected readonly dayLabel = computed(() =>
    new Date(`${this.day()}T00:00:00`).toLocaleDateString(undefined, {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
    }),
  );
  /** Each due item joined to its claim, so the drawer shows the work: who, where, what happened, who has it. */
  protected readonly dayCards = computed(() => {
    const byId = new Map((this.query.data() ?? []).map((c) => [c.id, c]));
    return this.dayItems().map((i: DueItem) => {
      const c = byId.get(i.id);
      return {
        ...i,
        market: c?.market ?? '',
        line: c?.line ?? '',
        claimant: c?.claimantName ?? 'Claimant',
        description: c?.incidentDescription ?? '',
        officer: c?.assignedOfficerName ?? 'Unassigned',
      };
    });
  });
  protected readonly overdueCount = computed(() => this.dayCards().filter((i) => i.overdue).length);
  /** The week strip in the drawer: each day with how many claims fall due on it. */
  protected readonly week = computed(() => {
    const d = this.day();
    if (!d) return [];
    const claims = this.query.data() ?? [];
    return weekOf(d).map((key) => {
      const date = new Date(`${key}T00:00:00`);
      const count = claimsDueOn(claims, key).length;
      return {
        key,
        count,
        dow: date.toLocaleDateString(undefined, { weekday: 'short' }),
        num: date.getDate(),
        label: `${date.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })}, ${count} due`,
      };
    });
  });
  protected shift(day: string, days: number): string {
    return shiftDay(day, days);
  }
  protected closeDay(): void {
    this.day.set('');
  }

  protected pickDay(value: string): void {
    this.day.set(value);
    this.focusPanel();
  }

  private focusPanel(): void {
    setTimeout(() => this.element.nativeElement.querySelector<HTMLElement>('[data-testid="day-panel"]')?.focus());
  }

  protected readonly query = injectQuery(() => ({
    queryKey: [...claimKeys.all, 'calendar', this.assigned()] as const,
    queryFn: () => fetchOpenClaims(this.api, this.assigned()),
  }));

  protected readonly events = computed(() => calendarEvents(this.query.data() ?? [], STATUS_TONE));

  constructor() {
    // The grid is built once its stylesheets are in AND the first claims have settled, so its first layout already includes
    // the event bars. Adding them afterwards re-lays out the day cells inside FullCalendar's own ResizeObserver callback,
    // which the browser reports as "ResizeObserver loop completed with undelivered notifications" (an error in the console).
    effect(() => {
      if (this.cssLoaded() && !this.query.isPending()) untracked(() => this.createCalendar());
    });
    // Events follow the data once the calendar exists.
    effect(() => {
      const events = this.events();
      if (this.ready()) this.calendar?.setOption('events', events);
    });
  }

  /** FullCalendar's stylesheets are vendor assets loaded with this route. */
  async ngAfterViewInit(): Promise<void> {
    await Promise.all(CALENDAR_CSS.map(loadStylesheet));
    this.cssLoaded.set(true);
  }

  private createCalendar(): void {
    const host = this.element.nativeElement.querySelector<HTMLElement>('.cal-host');
    if (!host || this.calendar) return;
    this.calendar = new Calendar(host, {
      plugins: [classicTheme, dayGridPlugin, timeGridPlugin, interactionPlugin],
      initialView: 'dayGridMonth',
      headerToolbar: { start: 'prev,next today', center: 'title', end: 'dayGridMonth,timeGridWeek' },
      height: 'auto',
      events: this.events(),
      dateClick: (arg) => {
        this.day.set(dayKey(arg.date));
        this.focusPanel();
      },
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
