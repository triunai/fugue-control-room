import { ActivatedRouteSnapshot } from '@angular/router';

export interface Crumb {
  label: string;
  /** Absent on the section and on the current page: they are not links. */
  link?: string;
}

/** Home › Section › Page from the deepest route's `data.section` / `data.breadcrumb`. Only Home is a link. */
export function deriveCrumbs(root: ActivatedRouteSnapshot): Crumb[] {
  let section: string | undefined;
  let page: string | undefined;
  for (let r: ActivatedRouteSnapshot | null = root; r; r = r.firstChild) {
    section = (r.data?.['section'] as string | undefined) ?? section;
    page = (r.data?.['breadcrumb'] as string | undefined) ?? page;
  }
  const crumbs: Crumb[] = [{ label: 'Home', link: '/' }];
  if (page) {
    if (section) crumbs.push({ label: section });
    crumbs.push({ label: page });
  }
  return crumbs;
}
