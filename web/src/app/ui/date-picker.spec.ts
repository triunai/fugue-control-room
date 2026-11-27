import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { addDays, addMonths, DatePicker, parseDate, parseTime } from './date-picker';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePicker],
  template: `
    <label for="d-when">Incident date</label>
    <fg-date inputId="d-when" [formControl]="control" [min]="min" [max]="max" />
  `,
})
class Host {
  readonly control = new FormControl('', { nonNullable: true, validators: Validators.required });
  min: string | null = '2026-01-10';
  max: string | null = '2026-03-20';
}

async function render() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  await fixture.whenStable();
  const el: HTMLElement = fixture.nativeElement;
  const field = el.querySelector<HTMLInputElement>('input#d-when')!;
  const toggle = el.querySelector<HTMLButtonElement>('button[aria-haspopup=dialog]')!;
  const settle = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const keyOn = async (target: Element, key: string, init: KeyboardEventInit = {}) => {
    target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init }));
    await settle();
  };
  const type = async (text: string) => {
    field.value = text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
    await settle();
  };
  const dialog = () => document.querySelector<HTMLElement>('[role=dialog]');
  return { fixture, host: fixture.componentInstance, field, toggle, settle, keyOn, type, dialog };
}

describe('date helpers', () => {
  it('parses ISO and day-first dates, and refuses impossible ones', () => {
    expect(parseDate('2026-02-28')).toBe('2026-02-28');
    expect(parseDate('7/3/2026')).toBe('2026-03-07');
    expect(parseDate('07.03.2026')).toBe('2026-03-07');
    expect(parseDate('2026-02-30')).toBeNull();
    expect(parseDate('2026-13-01')).toBeNull();
    expect(parseDate('2026-02')).toBeNull();
  });

  it('adds days across months and months with clamping', () => {
    expect(addDays('2026-02-28', 1)).toBe('2026-03-01');
    expect(addDays('2026-01-01', -1)).toBe('2025-12-31');
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-28');
    expect(addMonths('2028-01-31', 1)).toBe('2028-02-29');
    expect(addMonths('2026-03-15', -12)).toBe('2025-03-15');
  });
});

describe('DatePicker (fg-date)', () => {
  afterEach(() => document.querySelectorAll('.cdk-overlay-container').forEach((n) => (n.innerHTML = '')));

  it('CVA in: the control value shows in the field; the toggle names it', async () => {
    const { fixture, host, field, toggle } = await render();
    host.control.setValue('2026-02-14');
    fixture.detectChanges();
    expect(field.value).toBe('2026-02-14');
    // ICU versions differ on the comma after the weekday.
    expect(toggle.getAttribute('aria-label')).toMatch(/^Choose a date, Saturday,? 14 February 2026$/);
  });

  it('CVA out: typing an ISO or day-first date sets the ISO value; half a date is empty (like the native input)', async () => {
    const { host, field, type, settle } = await render();
    await type('2026-02-14');
    expect(host.control.value).toBe('2026-02-14');
    await type('2026-02');
    expect(host.control.value).toBe('');
    expect(host.control.hasError('required')).toBe(true);
    await type('3/2/2026');
    expect(host.control.value).toBe('2026-02-03');
    field.dispatchEvent(new FocusEvent('blur'));
    await settle();
    expect(field.value).toBe('2026-02-03');
    expect(host.control.touched).toBe(true);
  });

  it('keyboard: ArrowDown opens the calendar on the value, arrows and PageDown move, Enter picks', async () => {
    const { fixture, host, field, toggle, keyOn, dialog } = await render();
    host.control.setValue('2026-02-14');
    fixture.detectChanges();

    field.focus();
    await keyOn(field, 'ArrowDown');
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(dialog()).not.toBeNull();
    expect(dialog()!.querySelector('[role=grid]')).not.toBeNull();
    const focusedDay = () => dialog()!.querySelector<HTMLElement>('.fg-day[tabindex="0"]')!;
    expect(focusedDay().dataset['iso']).toBe('2026-02-14');
    expect(document.activeElement).toBe(focusedDay());
    expect(focusedDay().closest('td')!.getAttribute('aria-selected')).toBe('true');
    // Moving focus into the calendar is not leaving the field: no "required" error flashes behind the popover.
    expect(host.control.touched).toBe(false);

    await keyOn(focusedDay(), 'ArrowRight');
    expect(focusedDay().dataset['iso']).toBe('2026-02-15');
    await keyOn(focusedDay(), 'ArrowDown');
    expect(focusedDay().dataset['iso']).toBe('2026-02-22');
    await keyOn(focusedDay(), 'Home');
    expect(focusedDay().dataset['iso']).toBe('2026-02-16'); // Monday
    await keyOn(focusedDay(), 'PageDown');
    expect(focusedDay().dataset['iso']).toBe('2026-03-16');
    expect(dialog()!.textContent).toContain('March 2026');
    expect(document.activeElement).toBe(focusedDay());

    await keyOn(focusedDay(), 'Enter');
    expect(host.control.value).toBe('2026-03-16');
    expect(field.value).toBe('2026-03-16');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(field);
  });

  it('min/max: days outside are aria-disabled, keyboard focus stops at the bound, Escape closes unchanged', async () => {
    const { fixture, host, toggle, keyOn, dialog } = await render();
    host.control.setValue('2026-03-18');
    fixture.detectChanges();
    toggle.click();
    await fixture.whenStable();
    fixture.detectChanges();

    const day = (iso: string) => dialog()!.querySelector<HTMLElement>(`[data-iso="${iso}"]`)!;
    expect(day('2026-03-21').getAttribute('aria-disabled')).toBe('true');
    expect(day('2026-03-20').getAttribute('aria-disabled')).toBeNull();
    const focusedDay = () => dialog()!.querySelector<HTMLElement>('.fg-day[tabindex="0"]')!;
    await keyOn(focusedDay(), 'ArrowDown');
    expect(focusedDay().dataset['iso']).toBe('2026-03-20');
    expect(dialog()!.querySelector<HTMLButtonElement>('[aria-label="Next month"]')!.disabled).toBe(true);

    day('2026-03-21').click();
    fixture.detectChanges();
    expect(host.control.value).toBe('2026-03-18');

    await keyOn(focusedDay(), 'Escape');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(host.control.value).toBe('2026-03-18');
  });

  it('a click on a day picks it', async () => {
    const { fixture, host, toggle, dialog } = await render();
    host.control.setValue('2026-02-14');
    fixture.detectChanges();
    toggle.click();
    await fixture.whenStable();
    fixture.detectChanges();
    dialog()!.querySelector<HTMLElement>('[data-iso="2026-02-09"]')!.click();
    fixture.detectChanges();
    expect(host.control.value).toBe('2026-02-09');
  });

  it('disabled: the field and the toggle are disabled', async () => {
    const { fixture, host, field, toggle } = await render();
    host.control.disable();
    fixture.detectChanges();
    expect(field.disabled).toBe(true);
    expect(toggle.disabled).toBe(true);
  });
});

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DatePicker],
  template: `<fg-date inputId="t-at" [formControl]="control" [withTime]="true" />`,
})
class TimeHost {
  readonly control = new FormControl('', { nonNullable: true });
}

describe('DatePicker withTime', () => {
  async function renderTime() {
    const fixture = TestBed.createComponent(TimeHost);
    fixture.detectChanges();
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;
    const date = el.querySelector<HTMLInputElement>('input#t-at')!;
    const time = el.querySelector<HTMLInputElement>('input#t-at-time')!;
    const settle = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const typeIn = async (input: HTMLInputElement, text: string) => {
      input.value = text;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      await settle();
    };
    return { fixture, host: fixture.componentInstance, date, time, settle, typeIn };
  }

  it('parses times and refuses out-of-range ones', () => {
    expect(parseTime('09:05')).toBe('09:05');
    expect(parseTime('9:05')).toBe('09:05');
    expect(parseTime('23:59')).toBe('23:59');
    expect(parseTime('24:00')).toBeNull();
    expect(parseTime('12:60')).toBeNull();
    expect(parseTime('1230')).toBeNull();
  });

  it('value is YYYY-MM-DDTHH:mm, the datetime-local format, and empty until both parts are valid', async () => {
    const { host, date, time, typeIn } = await renderTime();
    await typeIn(date, '2026-02-14');
    expect(host.control.value).toBe('');
    await typeIn(time, '9:30');
    expect(host.control.value).toBe('2026-02-14T09:30');
    // Same instant the native input's value produced when sent to the API.
    expect(new Date(host.control.value).toISOString()).toBe(new Date(2026, 1, 14, 9, 30).toISOString());
    await typeIn(time, '24:00');
    expect(host.control.value).toBe('');
    expect(time.getAttribute('aria-invalid')).toBe('true');
    await typeIn(time, '23:59');
    expect(host.control.value).toBe('2026-02-14T23:59');
    expect(time.getAttribute('aria-invalid')).toBeNull();
  });

  it('CVA in: a datetime-local value fills both fields; null clears them', async () => {
    const { fixture, host, date, time } = await renderTime();
    host.control.setValue('2026-03-01T07:05');
    fixture.detectChanges();
    expect(date.value).toBe('2026-03-01');
    expect(time.value).toBe('07:05');
    host.control.setValue('');
    fixture.detectChanges();
    expect(date.value).toBe('');
    expect(time.value).toBe('');
  });

  it('keyboard: ArrowUp/Down step the time by a minute, Shift by an hour, wrapping the day', async () => {
    const { host, date, time, typeIn, settle } = await renderTime();
    await typeIn(date, '2026-02-14');
    await typeIn(time, '23:59');
    time.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true, cancelable: true }));
    await settle();
    expect(time.value).toBe('00:00');
    expect(host.control.value).toBe('2026-02-14T00:00');
    time.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', shiftKey: true, bubbles: true, cancelable: true }),
    );
    await settle();
    expect(time.value).toBe('23:00');
  });

  it('default mode has no time field', async () => {
    const { el } = { el: (await render()).fixture.nativeElement as HTMLElement };
    expect(el.querySelector('input[id$="-time"]')).toBeNull();
  });
});
