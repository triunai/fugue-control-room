import { NavigationError } from '@angular/router';
import { isStaleChunkError, reloadOnStaleChunk } from './stale-chunk';

describe('stale chunk after a redeploy', () => {
  it("recognises the browsers' messages for a lazy chunk that no longer exists", () => {
    expect(
      isStaleChunkError(new TypeError('Failed to fetch dynamically imported module: http://x/chunk-AB12.js')),
    ).toBe(true);
    expect(isStaleChunkError(new TypeError('Importing a module script failed.'))).toBe(true);
    expect(isStaleChunkError(new TypeError('error loading dynamically imported module'))).toBe(true);
    expect(isStaleChunkError(Object.assign(new Error('Loading chunk 42 failed.'), { name: 'ChunkLoadError' }))).toBe(
      true,
    );
  });

  it('leaves every other navigation error alone', () => {
    expect(isStaleChunkError(new Error('Cannot match any routes'))).toBe(false);
    expect(isStaleChunkError('NG04002')).toBe(false);
  });

  it('reloads the target URL only for a stale chunk', () => {
    const reload = vi.fn();
    reloadOnStaleChunk(
      new NavigationError(1, '/claims/abc', new TypeError('Failed to fetch dynamically imported module: x')),
      reload,
    );
    reloadOnStaleChunk(new NavigationError(2, '/nope', new Error('Cannot match any routes')), reload);
    expect(reload).toHaveBeenCalledTimes(1);
    expect(reload).toHaveBeenCalledWith('/claims/abc');
  });
});
