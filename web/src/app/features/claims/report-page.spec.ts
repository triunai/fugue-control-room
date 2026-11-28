import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { QueryClient, provideTanStackQuery } from '@tanstack/angular-query';
import { ClaimsApi } from '../../api/claims.api';
import { TourService } from '../shell/tour.service';
import { ReportPage } from './report-page';

describe('report page: the psst hint after a submit', () => {
  it('points at the search bar with the new claim number once the submit succeeded', async () => {
    const hint = vi.fn().mockResolvedValue(undefined);
    const submit = vi.fn().mockResolvedValue({ id: 'c1', number: 'CLM-SG-2026-000042' });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTanStackQuery(() => new QueryClient()),
        { provide: ClaimsApi, useValue: { submit } },
        { provide: TourService, useValue: { hint } },
      ],
    });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const page = TestBed.createComponent(ReportPage).componentInstance as unknown as {
      form: { patchValue(v: object): void };
      submit(): void;
    };
    page.form.patchValue({
      market: 'SG',
      line: 'Motor',
      policyNumber: 'POL-1',
      incidentDate: '2026-11-20',
      incidentDescription: 'Rear-ended at the lights.',
    });
    page.submit();
    await vi.waitFor(() => expect(hint).toHaveBeenCalledTimes(1));
    expect(hint).toHaveBeenCalledWith(
      '[data-tour="search"]',
      'psst 👀',
      'Try finding it: type CLM-SG-2026-000042 in the search bar (or press /).',
    );
  });

  it('does not hint when the submit fails', async () => {
    const hint = vi.fn();
    const submit = vi.fn().mockRejectedValue(new Error('boom'));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTanStackQuery(() => new QueryClient()),
        { provide: ClaimsApi, useValue: { submit } },
        { provide: TourService, useValue: { hint } },
      ],
    });
    const page = TestBed.createComponent(ReportPage).componentInstance as unknown as {
      form: { patchValue(v: object): void };
      submit(): void;
    };
    page.form.patchValue({
      market: 'SG',
      line: 'Motor',
      policyNumber: 'P',
      incidentDate: '2026-11-20',
      incidentDescription: 'x',
    });
    page.submit();
    await vi.waitFor(() => expect(submit).toHaveBeenCalled());
    await new Promise((r) => setTimeout(r, 20));
    expect(hint).not.toHaveBeenCalled();
  });
});
