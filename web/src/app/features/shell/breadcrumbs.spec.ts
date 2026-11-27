import { ActivatedRouteSnapshot } from '@angular/router';
import { deriveCrumbs } from './breadcrumbs';
import { healthOf } from './health';

const snap = (data: Record<string, unknown>, child: ActivatedRouteSnapshot | null = null) =>
  ({ data, firstChild: child }) as unknown as ActivatedRouteSnapshot;

describe('deriveCrumbs', () => {
  it('builds Home > Section > Page with only Home linked', () => {
    const root = snap({}, snap({}, snap({ section: 'Data', breadcrumb: 'Customers' })));
    expect(deriveCrumbs(root)).toEqual([{ label: 'Home', link: '/' }, { label: 'Data' }, { label: 'Customers' }]);
  });

  it('omits the section when the page has none', () => {
    expect(deriveCrumbs(snap({}, snap({ breadcrumb: 'Not found' })))).toEqual([
      { label: 'Home', link: '/' },
      { label: 'Not found' },
    ]);
  });

  it('is just Home when no route labels itself', () => {
    expect(deriveCrumbs(snap({}))).toEqual([{ label: 'Home', link: '/' }]);
  });
});

describe('healthOf', () => {
  it('maps query status to the footer dot', () => {
    expect(healthOf('success')).toBe('up');
    expect(healthOf('error')).toBe('down');
    expect(healthOf('pending')).toBe('checking');
  });
});
