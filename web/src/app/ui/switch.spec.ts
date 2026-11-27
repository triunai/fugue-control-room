import { TestBed } from '@angular/core/testing';
import { Switch } from './switch';

function render(inputs: Record<string, unknown>) {
  const fixture = TestBed.createComponent(Switch);
  for (const [k, v] of Object.entries(inputs)) fixture.componentRef.setInput(k, v);
  fixture.detectChanges();
  const el: HTMLElement = fixture.nativeElement;
  return { fixture, btn: el.querySelector('button')!, tip: el.querySelector('[role=tooltip]')! };
}

describe('Switch', () => {
  it('is on: aria-checked true, tooltip "Turn off", described by the tooltip', () => {
    const { btn, tip } = render({ checked: true, label: 'Switch Customers' });
    expect(btn.getAttribute('role')).toBe('switch');
    expect(btn.getAttribute('aria-checked')).toBe('true');
    expect(tip.textContent?.trim()).toBe('Turn off');
    expect(btn.getAttribute('aria-describedby')).toBe(tip.id);
  });

  it('is off: aria-checked false, tooltip "Turn on"', () => {
    const { btn, tip } = render({ checked: false, label: 'Switch Customers' });
    expect(btn.getAttribute('aria-checked')).toBe('false');
    expect(tip.textContent?.trim()).toBe('Turn on');
  });

  it('locked: "Always on", aria-disabled and no emit', () => {
    const { fixture, btn, tip } = render({ checked: true, locked: true, label: 'Switch System' });
    let emitted = 0;
    fixture.componentInstance.toggled.subscribe(() => emitted++);
    btn.click();
    expect(tip.textContent?.trim()).toBe('Always on');
    expect(btn.getAttribute('aria-disabled')).toBe('true');
    expect(emitted).toBe(0);
  });

  it('emits when usable and stays quiet while pending', () => {
    const { fixture, btn } = render({ checked: true, label: 'Switch Audit' });
    let emitted = 0;
    fixture.componentInstance.toggled.subscribe(() => emitted++);
    btn.click();
    expect(emitted).toBe(1);
    fixture.componentRef.setInput('pending', true);
    fixture.detectChanges();
    btn.click();
    expect(emitted).toBe(1);
  });
});
