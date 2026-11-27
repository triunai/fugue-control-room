import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Select, SelectOption } from './select';

const OPTIONS: SelectOption[] = [
  { value: 'SG', label: 'Singapore' },
  { value: 'MY', label: 'Malaysia' },
  { value: 'TH', label: 'Thailand', disabled: true },
  { value: 'ID', label: 'Indonesia' },
  { value: 'PH', label: 'Philippines' },
];

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, Select],
  template: `
    <label for="s-market">Market</label>
    <fg-select inputId="s-market" [formControl]="control" [options]="options" placeholder="Choose a market" />
  `,
})
class Host {
  readonly control = new FormControl('', { nonNullable: true, validators: Validators.required });
  readonly options = OPTIONS;
}

async function render() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  await fixture.whenStable();
  const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button[role=combobox]');
  const key = async (k: string, init: KeyboardEventInit = {}) => {
    trigger.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true, ...init }));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const listbox = () => document.querySelector<HTMLElement>('[role=listbox]');
  const activeOption = () => {
    const id = trigger.getAttribute('aria-activedescendant');
    return id ? document.getElementById(id) : null;
  };
  return { fixture, host: fixture.componentInstance, trigger, key, listbox, activeOption };
}

describe('Select (fg-select)', () => {
  afterEach(() => document.querySelectorAll('.cdk-overlay-container').forEach((n) => (n.innerHTML = '')));

  it('is a select-only combobox: role, haspopup, expanded and the placeholder while empty', async () => {
    const { trigger, listbox } = await render();
    expect(trigger.getAttribute('aria-haspopup')).toBe('listbox');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(trigger.textContent?.trim()).toBe('Choose a market');
    expect(listbox()).toBeNull();
  });

  it('CVA in: a value set on the control shows its label', async () => {
    const { fixture, host, trigger } = await render();
    host.control.setValue('MY');
    fixture.detectChanges();
    expect(trigger.textContent?.trim()).toBe('Malaysia');
  });

  it('keyboard: ArrowDown opens on the selected option, arrows skip disabled options, Enter commits (CVA out)', async () => {
    const { fixture, host, trigger, key, listbox, activeOption } = await render();
    host.control.setValue('MY');
    fixture.detectChanges();

    await key('ArrowDown');
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(listbox()?.getAttribute('role')).toBe('listbox');
    expect(trigger.getAttribute('aria-controls')).toBe(listbox()?.id);
    expect(activeOption()?.textContent).toContain('Malaysia');
    expect(activeOption()?.getAttribute('aria-selected')).toBe('true');

    await key('ArrowDown'); // Thailand is disabled: skipped
    expect(activeOption()?.textContent).toContain('Indonesia');
    await key('Enter');

    expect(host.control.value).toBe('ID');
    expect(host.control.dirty).toBe(true);
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(trigger.getAttribute('aria-activedescendant')).toBeNull();
    expect(trigger.textContent?.trim()).toBe('Indonesia');
  });

  it('Escape closes without changing the value; Home and End jump', async () => {
    const { host, trigger, key, activeOption } = await render();
    await key('End');
    expect(activeOption()?.textContent).toContain('Philippines');
    await key('Home');
    expect(activeOption()?.textContent).toContain('Singapore');
    await key('Escape');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(host.control.value).toBe('');
  });

  it('type-ahead: letters jump to the matching option, and Tab keeps it', async () => {
    const { host, key, activeOption } = await render();
    await key('p');
    expect(activeOption()?.textContent).toContain('Philippines');
    await key('Tab');
    expect(host.control.value).toBe('PH');
  });

  it('clicking an option commits it; leaving marks the control touched', async () => {
    const { fixture, host, trigger } = await render();
    trigger.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const opt = [...document.querySelectorAll<HTMLElement>('[role=option]')].find((o) =>
      o.textContent?.includes('Singapore'),
    )!;
    opt.click();
    fixture.detectChanges();
    expect(host.control.value).toBe('SG');
    expect(host.control.touched).toBe(false);
    trigger.dispatchEvent(new FocusEvent('blur'));
    expect(host.control.touched).toBe(true);
  });

  it('disabled: the trigger is disabled and keys do nothing', async () => {
    const { fixture, host, trigger, key } = await render();
    host.control.disable();
    fixture.detectChanges();
    expect(trigger.disabled).toBe(true);
    await key('ArrowDown');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
  });
});
