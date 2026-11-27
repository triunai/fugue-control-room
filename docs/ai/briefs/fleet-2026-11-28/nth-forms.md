# Lane nth-forms (item 5) — Opus — worktree .worktrees/nth-forms, branch nth-forms, base dacfb06

Objective: no native OS popups in any form, and every form can be filled with a realistic example in one click.

Current state (verify): forms use native `<select>` and `<input type="date">` which open OS-styled popups. The sign-in page
already has a "demo accounts" pattern that fills the form. A CDK dialog wrapper exists (`web/src/app/ui/overlays.ts`), an
inline icon component exists (`web/src/app/ui/icon.ts`). Find every form: report an incident, request info, set reserve,
decide/reject (reason), claimant answer, webhook subscription, customers create/edit, add user (Users & access drawer).

Desired state:
1. Shared `fg-select` (CDK Overlay + listbox: type-ahead, arrow keys, Enter/Escape, `aria-activedescendant`, works with
   Angular reactive forms via ControlValueAccessor, disabled + error states, console tokens) and `fg-date` (calendar popover
   on CDK Overlay, keyboard navigable, min/max, ISO value, manual typing allowed) in `web/src/app/ui/`, with specs.
2. Every native select/date in the forms above replaced. Validation messages and submit behaviour unchanged.
3. "Fill with an example" button on every form, styled like the sign-in demo accounts: Report an incident gets 3-4
   realistic templates per market/line (MY, SG, TH, ID, PH x Motor/Property — use the markets/lines the app really has),
   the others 1-3 each. Examples live in one data file (`web/src/app/ui/examples.ts` or per feature). Clearly sample data.

Acceptance: no `<select>` or `type="date"` left in those forms (grep proves it); keyboard-only run through the report form
works; axe-like checks: listbox roles/aria present; each example button fills valid values and the form submits (headless
against the demo API on :8080 is fine: submit one claim as self@fugue.test and one webhook subscription as admin); 0 console
errors; specs for select/date (keyboard, CVA value in/out).

Non-goals: no backend change, no new form fields, no restyle of pages beyond the controls, no edits to `features/hood/**`,
the claim header stepper / "How a claim moves" modal (nth-fixes), the modules page (nth-fixes), or import pages (nth-import).
Stop if: a form's validation lives in a way the new control breaks and you cannot keep behaviour identical — report it.
Commits: 1 fg-select + spec, 2 fg-date + spec, 3 swap into forms, 4 examples + buttons.

Recite: shared accessible select and date controls, every form swapped, an example filler per form, behaviour unchanged.
