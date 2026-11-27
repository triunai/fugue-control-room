# Lane nth-polish status

Branch `nth-polish`, base 8a103cc. Refs W-022, D-059.

## Commits
- 3f633b1 `feat(web): Univer sheets with the full toolbar and formula bar`
- 94aa8ed `fix(web): one type family in breadcrumbs and content group headers`
- 8281dfc `test(web): screenshots of the sheets and the matrix`

## What changed
- Both sheets (reserve sheet, claim assessment worksheet) mount Univer with header (Start / Formulas / View), toolbar,
  formula bar (preset default) and context menu on; footer off. Light theme, en-US locale.
- Frame with rounded corners; caption under the sheet; actions moved to a footer row.
- Editable cells tinted with a new solid token `--sheet-input` (tokens.css; the canvas cannot blend an alpha tint). Read-only
  elsewhere through the existing `BeforeSheetEditStart` cancel guard (reserve column; assessment inputs and excess).
- Rows are the data plus the header (`sheetRowCount`), host height fits the chrome plus the rows (`sheetHostHeight`),
  numeric columns right-aligned, wider claim and label columns. Helpers live in `sheet-layout.ts` (unit tested).
- Breadcrumb: one UI-sans size, current page semibold (no display serif, no letter-spaced accent caps).
- Other uppercase letter-spaced in-content labels found by grep: `.hero-kicker` (home) and `.track-head` (claim banner);
  both now quiet sentence-case UI sans. Sidebar group labels left alone as instructed.

## Counts
- `npm test`: 29 files, 162 tests passed (was 159 on main). `npm run lint`: clean. `npm run build`: green.
- Initial bundle before: main 384.26 kB, styles 42.21 kB, total 426.48 kB raw / 112.17 kB transfer.
  After: main 384.26 kB, styles 42.24 kB, total 426.50 kB raw / 112.24 kB transfer. Univer stays lazy
  (`univer-loader` chunk 6.84 MB before, 6.83 MB after). Deviation: initial grew by 0.02 kB raw (one token and CSS rules).

## Verified in a real browser
Throwaway Postgres :5459, API :5109 seeded, `ng serve` :4209 with a proxy config kept outside the repo. Screenshots:
`web/docs/screenshots/claims-sheet.png`, `claim-assessment.png`, `access-matrix.png` (breadcrumb visible in each). The reference
sheet was screenshotted into scratch only. Containers and processes stopped by port.

## Deviations and not verified
- Read-only is the edit guard (cancelled edit start), not Univer sheet protection; a paste into a non-editable cell is not blocked
  (unchanged from before).
- The toolbar lets a user restyle cells (also true of the reference); styles are not saved.
- Tinting applies to non-empty reserve cells only: a row with no reserve yet (null) has an untinted reserve cell, still editable.
- The first, whole-column highlight on A1 is Univer's normal active-cell header highlight; it is not a full-column selection.
- Not verified: mobile widths, dark theme (non-goal), a save round trip after this change (logic untouched, unit tests green).
