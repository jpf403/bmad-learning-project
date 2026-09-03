---
title: 'NavBar: true-center nav links, group hamburger with account controls'
type: 'bugfix'
created: '2026-09-02'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '6441a44fcf2c11f4517f87b7d605b935c8848031'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `NavBar` lays out `logo | links | actions` with `justify-content: space-between`. Because the logo ("Fake Barbershop") and the actions area (account icon, or Sign In/Register) are different widths, the middle nav-links group (and, below 1023px, the hamburger menu button that replaces it) isn't visually centered on the page — it sits wherever `space-between` happens to land it given the unequal side widths. Below 1023px, the hamburger button also floats as its own separate item between the logo and the account controls instead of sitting next to them.

**Approach:** Switch `.nav-bar` from flex/`space-between` to a 3-column CSS grid (`1fr auto 1fr`) so the middle column is always centered on the full bar width regardless of the side columns' content width. Move the hamburger button's markup inside `.nav-bar__actions` (as its first child) so at any width where it's visible, it sits directly next to the account icon / Sign In+Register buttons in the same flex group, instead of as a separate top-level nav-bar child.

## Boundaries & Constraints

**Always:**
- Preserve all three existing breakpoints and their current trigger widths: full links row ≥1024px, collapsed-to-hamburger 640–1023px, full column stack ≤639px.
- Preserve every existing CSS class name (`nav-bar__logo`, `nav-bar__links`, `nav-bar__menu-button`, `nav-bar__menu-dropdown`, `nav-bar__actions`, etc.) — only reposition markup and adjust layout properties, do not rename classes or restructure the Radix `DropdownMenu.Root`/`Trigger`/`Portal`/`Content` composition for either dropdown.
- Keep the hamburger's own dropdown (nav links) and the account dropdown as two independent `DropdownMenu.Root` instances, exactly as today — do not merge them into one menu.
- No behavior/logic changes to `NavBar.jsx` beyond relocating the hamburger `DropdownMenu.Root` JSX block — role-gating, active-link logic, and logout handling are untouched.

**Ask First:** none anticipated — this is layout-only CSS plus a pure JSX relocation, using the existing design-token variables already present in `NavBar.css` (`--spacing-*`, `--rounded-*`, `--color-*`). If achieving true centering requires a token/variable not already used in this file, stop and ask before introducing one.

**Never:**
- Do not touch `NavBar.test.jsx`'s existing assertions' meaning — tests query by role/text, not DOM position, so none should need behavior changes, but re-run the full suite to confirm.
- ~~Do not change the ≤639px full-stack behavior's intent...~~ **Renegotiated (2026-09-02, Jack)**: at ≤639px, the signed-in state (hamburger + compact profile icon, both fixed 40×40) must stay in a single row at full height instead of stacking — there's no overflow risk to justify it. The signed-out state (Sign In/Register, deliberately full-width/stacked for larger tap targets) is explicitly unchanged.
- Do not introduce a new npm dependency or a different centering technique (e.g. absolute positioning, JS-measured widths) — CSS Grid is sufficient and is the approach to use.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Desktop, signed out | Viewport ≥1024px, no user | Logo left, Home/About/etc. links horizontally centered on the bar, Sign In + Register right | N/A |
| Desktop, signed in as Admin | Viewport ≥1024px, Admin user (longest link set) | Links (incl. Admin Panel) still centered as a group; centering doesn't shift because the group is wider | N/A |
| Tablet/narrow, collapsed | Viewport 640–1023px | Links list hidden; hamburger button renders immediately next to (before) the account icon / Sign In+Register, both right-aligned together | N/A |
| Tablet/narrow, hamburger opened | Same as above, hamburger clicked | Dropdown of nav links opens below the hamburger button, unaffected by its new position | N/A |
| Mobile stacked, signed out | Viewport ≤639px, no user | Bar stacks to a column as today; Sign In/Register remain full-width and stacked | N/A |
| Mobile stacked, signed in | Viewport ≤639px, signed-in user | Logo, hamburger, and profile icon stay in a single row at full (64px) height — no stacking | N/A |

</frozen-after-approval>

## Code Map

- `frontend/src/components/NavBar.jsx` — move the hamburger `DropdownMenu.Root` (menu button + its dropdown of nav links) from a top-level sibling of `.nav-bar__actions` to the first child inside `.nav-bar__actions`.
- `frontend/src/components/NavBar.css` — change `.nav-bar`'s base layout from `display:flex; justify-content:space-between` to `display:grid; grid-template-columns: 1fr auto 1fr;` with `justify-self` on `.nav-bar__logo` (start), `.nav-bar__links` (center), `.nav-bar__actions` (end); verify the ≤639px override still fully replaces this with the existing column-stack flex layout.
- `frontend/src/components/NavBar.test.jsx` — no assertion changes expected (role/text-based queries); re-run as a regression check.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/components/NavBar.jsx` -- relocate the hamburger `DropdownMenu.Root` block into `.nav-bar__actions` as its first child -- groups it with the account controls at every width it's visible
- [x] `frontend/src/components/NavBar.css` -- convert `.nav-bar` to a 3-column grid (`1fr auto 1fr`) with `justify-self` on each of the three direct children; the `@media (max-width: 639px)` override did NOT already set `display:flex` (spec's assumption was wrong) -- added `display: flex;` there explicitly, since leaving the grid active at mobile widths would have broken the stack -- makes the middle column's centering independent of the logo/actions width mismatch
- [x] `frontend/src/components/NavBar.css` + `NavBar.jsx` -- adversarial review caught a real bug: no `grid-column` meant hiding `.nav-bar__links` at 640–1023px would auto-place `.nav-bar__actions` into the middle column instead of the right one -- pinned `grid-column: 1/2/3` explicitly on all three children; added `minmax(0, ...)` overflow guard on the two flanking tracks
- [x] `frontend/src/components/NavBar.jsx` + `NavBar.css` -- follow-up (2026-09-02, Jack): added a `nav-bar--signed-in` modifier class so the ≤639px signed-in layout (hamburger + profile icon) stays a single row at full height instead of stacking; signed-out (Sign In/Register) behavior is untouched

**Acceptance Criteria:**
- Given a viewport ≥1024px, when the page renders, then the nav links are horizontally centered on the full width of the bar regardless of logo/actions width difference.
- Given a viewport between 640px and 1023px, when the page renders, then the hamburger button appears immediately adjacent to the account icon (signed in) or Sign In/Register buttons (signed out), not floating separately, and `.nav-bar__actions` stays pinned to the right-hand column even though `.nav-bar__links` is hidden.
- Given the hamburger button in its new position, when clicked, then its dropdown of nav links still opens and behaves exactly as before (existing `NavBar.test.jsx` "collapsed navigation menu" tests pass unmodified).
- Given a viewport ≤639px and a signed-in user, when the page renders, then the logo, hamburger, and profile icon stay in a single row at full height — no stacking.
- Given a viewport ≤639px and no signed-in user, when the page renders, then the bar still stacks into a column with Sign In/Register full-width, exactly as before this spec.

## Spec Change Log

## Design Notes

CSS Grid with `1fr auto 1fr` is the standard technique for a truly-centered middle nav item regardless of unequal side content: the two `1fr` tracks are forced equal, so the `auto` middle track (sized to the links' own content width) is mathematically centered on the full row — unlike flex `space-between`, which only guarantees equal *gaps*, not a centered middle item, when the two outer items differ in width. `justify-self` positions each item within its own grid cell (not `justify-content`, which controls the grid container's own extra space distribution and isn't needed here since the two `1fr` tracks already consume all remaining space).

## Verification

**Commands:**
- `npx vitest run src/components/NavBar.test.jsx` -- expected: all existing tests pass unmodified
- `npx eslint .` and `npx prettier --check .` on changed files -- expected: clean

**Manual checks (if no CLI):**
- View the app in a browser at ≥1024px, ~800px, and ~400px widths and confirm: links centered on the full bar at desktop width; hamburger sits next to account controls at tablet width; column stack with no overflow at mobile width. **Not screenshot-verified** — no browser-automation tooling set up in this environment (matches this project's existing pattern for CSS-only changes); Jack to spot-check visually. All non-visual acceptance criteria (dropdown behavior unchanged, no JS regressions) are confirmed via the automated test suite below.

## Suggested Review Order

**Grid layout & the auto-placement bug**

- Root-cause fix from adversarial review: without explicit `grid-column`, hiding `.nav-bar__links` at 640–1023px would auto-place `.nav-bar__actions` into the middle column instead of the right one.
  [`NavBar.css:1-3`](../../frontend/src/components/NavBar.css#L1)

- Each of the three children pinned to its column explicitly — makes centering independent of which children are hidden at any breakpoint.
  [`NavBar.css:16-17`](../../frontend/src/components/NavBar.css#L16), [`NavBar.css:27,31`](../../frontend/src/components/NavBar.css#L27), [`NavBar.css:59-60`](../../frontend/src/components/NavBar.css#L59)

**Hamburger relocation**

- Hamburger's `DropdownMenu.Root` moved inside `.nav-bar__actions` as its first child, grouping it with the account controls whenever visible.
  [`NavBar.jsx:67-70`](../../frontend/src/components/NavBar.jsx#L67)

**Mobile signed-in single-row follow-up**

- `nav-bar--signed-in` modifier class, added only when a user is signed in.
  [`NavBar.jsx:49`](../../frontend/src/components/NavBar.jsx#L49)

- Overrides the ≤639px column-stack back to a single row at full height for that modifier only; signed-out stacking is untouched.
  [`NavBar.css:129-138`](../../frontend/src/components/NavBar.css#L129)

**Tests**

- Structural regression tests locking in the JSX relocation and the signed-in/signed-out modifier split (DOM-nesting/class only — real centering/spacing still needs a manual browser check).
  [`NavBar.test.jsx:184`](../../frontend/src/components/NavBar.test.jsx#L184), [`NavBar.test.jsx:222`](../../frontend/src/components/NavBar.test.jsx#L222), [`NavBar.test.jsx:279`](../../frontend/src/components/NavBar.test.jsx#L279)
