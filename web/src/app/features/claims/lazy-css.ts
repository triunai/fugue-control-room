/**
 * Loads a vendor stylesheet (copied to `vendor/` by angular.json `assets`) the first time a lazy page needs it.
 * The big library stylesheets are not part of any JS or CSS bundle, so the initial download never carries them.
 */
const loaded = new Map<string, Promise<void>>();

export function loadStylesheet(href: string): Promise<void> {
  let p = loaded.get(href);
  if (!p) {
    p = new Promise<void>((resolve, reject) => {
      const link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = href;
      link.onload = () => resolve();
      link.onerror = () => reject(new Error(`Could not load ${href}`));
      document.head.appendChild(link);
    });
    loaded.set(href, p);
  }
  return p;
}
