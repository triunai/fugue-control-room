import { onNavigationEnd } from './nav-progress';

describe('navigation progress phases', () => {
  it('a navigation that ends before the bar appeared stays idle (nothing flashes)', () => {
    expect(onNavigationEnd('idle')).toBe('idle');
  });

  it('a visible bar finishes, a finished bar stays finishing', () => {
    expect(onNavigationEnd('running')).toBe('finishing');
    expect(onNavigationEnd('finishing')).toBe('finishing');
  });

  it('a bar already fading goes back to idle', () => {
    expect(onNavigationEnd('fading')).toBe('idle');
  });
});
