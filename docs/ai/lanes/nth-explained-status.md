# Lane nth-explained status

## Commits
- see `git log nth-explained` (single slice)

## What changed in shared files (exactly)
- `README.md`: one paragraph before "Where things live".
- `web/src/app/features/shell/help-button.ts`: one link "Fugue, explained" beside the Help pill (opens `/explained/index.html` in a new tab).

## Deviations
- The Help control is a single pill, not a menu, so the link sits next to it.
- No row for this lane exists in `docs/branch-ledger.md`; none added.
- The page loads mermaid from `/_runtime/mermaid-11.16.1.min.js` (an artifact-host path). Added `web/public/_runtime/mermaid-11.16.1.min.js` (mermaid 11.16.0 copied from a local npm cache, 3.5 MB; outside the lane file list) so the page has 0 console errors. Fonts still come from Google Fonts (system fallback offline).

## Slice 2: restyle (owner addendum 11:30)
- `docs/explained/source.html` is the verbatim fetch; `docs/explained/index.html` and `web/public/explained/index.html` are the restyled copy (console token values, navy headings, gold rules and rail labels, surface frame with the page as a rounded card, light default + dark media query, diagram colours set to tokens). Content unchanged.
- Headless: light and dark, 6 diagrams rendered, 0 console errors. Screenshots in `.worktrees/_briefs/reports/nth-explained-{light,dark}-{top,claim}.png`.
- Known: in dark, some mermaid labels clip by a letter (text measured before the web font loads); the "request info" label overlaps its edge in both themes (mermaid layout).

## Slice 3 (mermaid notice)
- Added `web/public/_runtime/NOTICE.md` (MIT, mermaid 11.16.0 copied under the 11.16.1 name). Kept: the page needs it to render diagrams offline.

## Slice 4: nav item (owner 11:35)
- Worktree re-created from main b55270a (the first one was retired after merge); 805a298 (NOTICE) cherry-picked since it was not on main.
- `nav.ts`: new optional `external` flag and a "Fugue, explained" item in Platform (no permission: every persona). `sidebar.html`/`sidebar.ts`/`sidebar.css`: external items render as a plain new-tab link with an external-link icon (shared item body via ng-template). Files beyond the lane list: sidebar.* (needed to render a non-route link).
- Gates: lint pass; tests 58 files / 370 passed; build complete. Headless as claimant and admin: item visible, click opens /explained/index.html, heading visible, 0 console errors. Shot: `.worktrees/_briefs/reports/nth-explained-nav-self.png`.
- Help-pill link got `white-space: nowrap` (it wrapped in the top bar).
