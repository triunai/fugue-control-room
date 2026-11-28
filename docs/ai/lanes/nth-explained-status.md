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
