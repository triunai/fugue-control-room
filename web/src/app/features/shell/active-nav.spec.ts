import { activeItem } from './active-nav';
import { NAV } from './nav';

describe('activeItem', () => {
  it('picks the longest item path that is the URL or one of its parents', () => {
    expect(activeItem(NAV, '/claims/queue')).toEqual({ group: 'Claims', path: '/claims/queue' });
    expect(activeItem(NAV, '/claims/mine')?.path).toBe('/claims/mine');
  });

  it('ignores query string and hash, and finds the group of a detail page', () => {
    expect(activeItem(NAV, '/audit?entityId=1#top')).toEqual({ group: 'Data', path: '/audit' });
    expect(activeItem(NAV, '/customers/abc-123')).toEqual({ group: 'Data', path: '/customers' });
  });

  it('is null where no nav item owns the URL', () => {
    expect(activeItem(NAV, '/unavailable')).toBeNull();
    expect(activeItem(NAV, '/customersx')).toBeNull();
  });
});
