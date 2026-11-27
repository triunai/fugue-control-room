import { NavGroup } from './nav';

/** The nav item a URL belongs to: the longest item path that is the URL or one of its parents, ignoring query and hash. */
export function activeItem(groups: NavGroup[], url: string): { group: string; path: string } | null {
  const path = url.split(/[?#]/)[0];
  let best: { group: string; path: string } | null = null;
  for (const g of groups) {
    for (const i of g.items) {
      const hit = path === i.path || path.startsWith(i.path + '/');
      if (hit && (!best || i.path.length > best.path.length)) best = { group: g.label, path: i.path };
    }
  }
  return best;
}
