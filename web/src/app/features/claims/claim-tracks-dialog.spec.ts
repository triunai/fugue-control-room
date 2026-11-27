import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { ClaimTracksDialog } from './claim-tracks-dialog';

function render(staff: boolean): string {
  TestBed.configureTestingModule({
    providers: [
      { provide: DialogRef, useValue: { close: () => undefined } },
      { provide: DIALOG_DATA, useValue: { staff } },
    ],
  });
  const f = TestBed.createComponent(ClaimTracksDialog);
  f.detectChanges();
  return (f.nativeElement as HTMLElement).textContent ?? '';
}

describe('ClaimTracksDialog', () => {
  it('shows staff the Reserved step', () => {
    expect(render(true)).toContain('Reserved');
  });

  it('shows claimants Assessed and never the reserve wording', () => {
    TestBed.resetTestingModule();
    const text = render(false);
    expect(text).toContain('Assessed');
    expect(text).not.toContain('Reserved');
    expect(text.toLowerCase()).not.toContain('set aside');
  });
});
