import { TestBed } from '@angular/core/testing';
import { ExamplesPanel } from './examples-panel';

describe('ExamplesPanel (fg-examples)', () => {
  it('labels itself as sample data, renders one button per row and emits the picked id', () => {
    const fixture = TestBed.createComponent(ExamplesPanel);
    fixture.componentRef.setInput('rows', [
      { id: 'a', title: 'Rear-ended at a junction', tag: 'SG · Motor', detail: '2 days ago' },
      { id: 'b', title: 'Burst pipe' },
    ]);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const section = el.querySelector('section')!;
    expect(document.getElementById(section.getAttribute('aria-labelledby')!)?.textContent).toContain(
      'Fill with an example',
    );
    expect(el.textContent).toContain('Sample data');
    const buttons = el.querySelectorAll<HTMLButtonElement>('button');
    expect(buttons.length).toBe(2);
    expect(buttons[0].textContent).toContain('SG · Motor');
    let picked = '';
    fixture.componentInstance.picked.subscribe((id) => (picked = id));
    buttons[1].click();
    expect(picked).toBe('b');
  });
});
