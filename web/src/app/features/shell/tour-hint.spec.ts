import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccessService } from '../../core/auth/access.service';
import { TourService } from './tour.service';

const drive = vi.fn();
const driver = vi.fn().mockImplementation(() => ({ drive, destroy: vi.fn() }));
vi.mock('driver.js', () => ({ driver: (cfg: unknown) => driver(cfg) }));

function service(): TourService {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AccessService, useValue: { has: () => false, me: () => null } }],
  });
  return TestBed.inject(TourService);
}

describe('TourService.hint', () => {
  beforeEach(() => {
    drive.mockClear();
    driver.mockClear();
    sessionStorage.clear();
    document.body.innerHTML = '';
    window.matchMedia = ((q: string) => ({ matches: false, media: q })) as typeof window.matchMedia;
  });

  function addTarget(visible = true): void {
    const el = document.createElement('input');
    el.setAttribute('data-tour', 'search');
    if (!visible) el.style.display = 'none';
    document.body.append(el);
    if (visible) el.getClientRects = () => [{}] as unknown as DOMRectList;
  }

  it('drives one popover on the selector with the title and text', async () => {
    addTarget();
    await service().hint('[data-tour="search"]', 'psst 👀', 'Try it.');
    expect(driver).toHaveBeenCalledTimes(1);
    const cfg = driver.mock.calls[0][0] as {
      steps: { element: string; popover: { title: string; description: string } }[];
    };
    expect(cfg.steps).toHaveLength(1);
    expect(cfg.steps[0].element).toBe('[data-tour="search"]');
    expect(cfg.steps[0].popover.title).toBe('psst 👀');
    expect(cfg.steps[0].popover.description).toBe('Try it.');
    expect(drive).toHaveBeenCalled();
  });

  it('stays silent when the element is not on screen', async () => {
    await service().hint('[data-tour="search"]', 't', 'd');
    expect(driver).not.toHaveBeenCalled();
  });

  it('stays silent under ?tour=off', async () => {
    addTarget();
    sessionStorage.setItem('fugue.tour.off', '1');
    await service().hint('[data-tour="search"]', 't', 'd');
    expect(driver).not.toHaveBeenCalled();
  });
});
