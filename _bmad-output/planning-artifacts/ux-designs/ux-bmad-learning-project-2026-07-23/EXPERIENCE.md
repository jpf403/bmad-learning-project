---
name: Fake Barbershop
status: final
sources:
  - "{planning_artifacts}/prds/prd-bmad-learning-project-2026-07-21/prd.md"
  - "{planning_artifacts}/prds/prd-bmad-learning-project-2026-07-21/addendum.md"
updated: 2026-07-24
---

# Fake Barbershop — Experience Spine

Fake Barbershop is a portfolio/learning project built as a real, working appointment scheduler — not a mockup. This document is the behavioral and information-architecture spine: what happens when someone clicks, taps, submits, or waits. `DESIGN.md` owns the visual identity (color, type, shape, elevation) and is referenced here by `{path.to.token}` name wherever a behavior needs to point at a visual spec; nothing visual is restated in this file.

## Foundation

Single-surface responsive web — mobile and desktop viewports, no native app (FR22/NFR3). React frontend, fully custom CSS/components except three Radix UI primitives (calendar/date-picker, dropdown/select, modal/dialog) that supply correct keyboard, focus, and ARIA behavior out of the box. One shop, one tenant, no multi-location concept (explicit non-goal) — three roles share the same app: **customer** (self-registers), **barber** (admin-created only, never self-registers), and a single **admin/owner** account, seeded once at server startup and never created, edited, or deleted through any UI (FR31/FR34). Every screen in the product is reachable by exactly one of these three roles' permitted surfaces; role visibility is enforced by hiding navigation entirely, not by showing-then-blocking (FR3).

The product's core goal is speed and certainty: a customer should be able to land on the site, book a slot, and leave certain the booking exists, in under a minute (SM8). Every friction-looking state below (a mismatch error, a conflict error, a "no barbers available" message) reflects a real correctness check, not a shortcut skipped for a smoother-looking demo.

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Home | Logo / unauthenticated landing | Hero, tagline, primary CTA into booking |
| Login | Nav "Sign In" (signed out) / redirect from any protected surface when signed out | Email + password sign-in |
| Register (Create Account) | Nav "Register" (signed out) / Home | Self-service customer signup (email, password ×2, first name, last name) |
| Schedule Appointment | Nav link (hidden signed-out, FR3) / customer's default landing on sign-in (FR4) / Home CTA when signed in | Booking form (barber, date, time) + "My Appointments" list of the signed-in user's own bookings |
| My Schedule | Nav link (hidden unless barber-or-admin) / barber-or-admin's default landing on sign-in (FR4) | Barber's own day, by date; admin sees the identical view plus a Select Barber dropdown |
| Admin Panel | Nav link (hidden unless admin) | Account search, result list, edit/create/delete popup |
| Account | Profile-icon dropdown → "Account" | Self-service edit of own first name, last name, password (not email, FR28) |
| About | Nav link | Static shop location, phone number, list of barbers (FR21) |
| Confirmation screen | Successful Submit on Schedule Appointment | Full-page replacement of the booking form — not a popup — so a second Submit is structurally impossible (FR9) |
| Confirm-action popup | Any cancel / account-edit-save / account-delete action | Universal two-button confirm gate before an irreversible or committing action (FR18/FR25–FR27/FR28/FR30/FR40) |

→ Composition reference: `mockups/home.html`, `mockups/schedule-appointment.html` (booking form + confirmation-screen states), `mockups/my-schedule.html` (admin variant), `mockups/confirm-popup.html` (destructive + non-destructive variants). `DESIGN.md` and `EXPERIENCE.md` win on conflict with any of these four mocks.

Nav bar appears on every page (`{components.nav-bar}`): wordmark left; Home / Schedule Appointment / About / My Schedule / Admin Panel next; Sign In + Register on the right when signed out, or a profile icon opening a dropdown (Account, Logout) when signed in (FR29). "My Schedule" and "Admin Panel" are hidden per-role per FR3 — full DOM-removal rule in Component Patterns, below.

A footer (`{components.footer}`) also appears on every page, below all content — static, identical regardless of role or auth state, no role-based variation the way the nav bar has.

Popup stacking: at most one popup is open at a time, with one deliberate exception. The Admin Panel's account-edit/create popup can have a confirm-action popup open on top of it (triggered by its Save or Delete action) — a single level of stacking, never two confirm popups nested. Clicking "Go Back" on the inner confirm popup returns to the edit popup with all fields exactly as left; only "Confirm" commits and closes both. *(This one-level-stack shape isn't spelled out verbatim in the memlog — it's the most literal reading of "the same popup shell is reused for edit and delete, differing only in Confirm's color" from DESIGN.md, and is flagged here as the interpretive default.)*

## Voice and Tone

Clean and professional: neutral, plain-spoken, no jokes, no exclamation points, ever — across every role. A barber's schedule and an admin's delete-confirm read in the same register as a customer's booking confirmation; Fake Barbershop doesn't get more casual for customers or more clinical for staff.

| Do | Don't |
|---|---|
| "Appointment booked with Manny at 11:00 AM on July 24." | "You're all set! 🎉 See you soon!" |
| "Invalid email or password." | "Hmm, that didn't work. Want to try again?" |
| "Passwords do not match." | "Oops! Your passwords don't match." |
| "No barbers available." | "No barbers yet — check back soon!" |
| "That time is no longer available. Choose another." | "Sorry, someone beat you to it :(" |
| "This appointment has already been cancelled." | "Looks like that one's already gone." |
| Same tone for customer, barber, and admin surfaces. | A friendlier voice for customers, a terser one for staff. |

## Component Patterns

Behavioral rules only — visual specs live in `DESIGN.md`, referenced below by token.

| Component | Use | Behavioral rules |
|---|---|---|
| Nav bar (`{components.nav-bar}`) | Every page | Role-based link visibility per FR3 (My Schedule hidden unless barber/admin, Admin Panel hidden unless admin) — removed from the DOM and tab order, not just visually hidden. Active link marked via `{components.nav-bar.link-foreground-active}`. Right side swaps Sign In/Register for the profile-icon dropdown based on auth state. |
| Footer (`{components.footer}`) | Every page | Static content, no role-based variation and no interactive elements — wordmark, address, phone, hours, copyright line only. Same on every page regardless of auth state, unlike the nav bar. |
| Profile icon dropdown | Nav, signed-in only | Radix-powered menu: Account, Logout. Logout ends the session server-side (FR23) — every open tab/device for that account is signed out immediately, not just the current tab. |
| Button — primary / secondary / destructive (`{components.button-primary}` / `{components.button-secondary}` / `{components.button-destructive}`) | Global | Destructive styling is reserved *only* for Cancel, Delete, and a destructive Confirm — never for emphasis or warnings. Hover/active color swap fires on pointer devices only. Every button is a single, complete tap on touch — no double-tap-to-arm, no press-and-hold. |
| Confirm-action popup (`{components.confirm-popup}`) | Cancel (appointment), Save (account edit), Delete (account) | Radix Dialog; exactly two buttons every time — "Go Back" (`{components.button-secondary}`, same neutral color regardless of what it cancels) and "Confirm," whose color is context-dependent: `{components.button-primary}` for a non-destructive save, `{components.button-destructive}` for cancel/delete. "Go Back" and outside-click/`Esc` all dismiss with zero effect. |
| Admin account-edit popup | Admin Panel, existing account rows | Fields: email, first name, last name, permission level (dropdown, customer/barber only — admin is never an option and the seeded admin account never appears as an editable row, FR17/FR34), password (double-entry, ×2 fields, optional — leaving both blank keeps the current password). Save routes through the confirm-action popup (non-destructive Confirm); Delete routes through it too (destructive Confirm). |
| Admin account-create popup | Admin Panel, "Create Account" action | Fields: email, first name, last name, password (double-entry, ×2 fields, required). No permission-level selector — creation always produces a barber account; there is no admin-creation or customer-creation path here (FR19). Save routes through the confirm-action popup (non-destructive Confirm). |
| Admin account row (`{components.admin-account-row}`) | Admin Panel search results | Click anywhere on the row opens the edit popup. Rows render only after a search returns matches (partial match on name or email, FR17); the single admin account is never a searchable/clickable row. |
| Booking form (barber / date / time / Submit), housed in `{components.form-section}` | Schedule Appointment | Barber select shows "No barbers available" in place of options if zero barber accounts exist (FR6). Calendar (`{components.calendar}`) disables past dates, dates beyond 30 days out, and weekends (FR7). Time dropdown (`{components.select-dropdown}`) lists only slots open for the chosen barber/date and, if the date is today, excludes anything within 30 minutes of the current time (FR8). Submit stays inactive until barber, date, and time are all chosen. |
| Confirmation screen (`{components.confirmation-screen}`) | Post-booking | Full-page replacement of the form (not a popup) — structurally prevents a duplicate submission (FR9). Copy pattern: "Appointment booked with {barber} at {time} on {date}." No name (the user is already signed in), no celebratory iconography. |
| My Appointments list | Schedule Appointment, below the form | Signed-in user's own upcoming bookings only, reusing `{components.schedule-row-booked}`'s visual treatment row-for-row (same surface, same name-plus-Cancel layout) rather than a distinct visual component. Past appointments are retained in the database but are not shown in this list at all — there is no "Finished" tag or past-appointment section (FR24, amended 2026-07-24). Each row carries a `{components.button-destructive}` Cancel that opens the confirm popup. |
| Date header + arrows | My Schedule | Two arrows (`{components.date-nav-arrow}`) step one day at a time; no jump-to-date control. Defaults to today on load. Weekends render as closed — no bookable slot grid — consistent with the calendar's weekend rule (FR7/FR13). |
| Barber-select dropdown, admin variant (`{components.select-dropdown-admin-barber}`) | My Schedule, admin only | Defaults to the first barber, auto-selected — never an empty state (FR15). Switching barbers re-renders the same visible date for the newly chosen barber; the date does not reset. |
| Schedule row — open (`{components.schedule-row-open}`) | My Schedule | Reads "Available." No action available on an open slot. |
| Schedule row — booked (`{components.schedule-row-booked}`) | My Schedule | Shows the customer's name plus a destructive "Cancel" button. Cancel opens the confirm popup; confirming flips the row back to "Available" and frees the slot for booking (FR25–FR27). |
| Home hero (`{components.button-primary}` CTA only — the graphic half isn't interactive) | Home | Signed-out click redirects to Login (FR5); signed-in click goes straight to Schedule Appointment. Same button, branching purely on auth state — no separate signed-in/out button variants. |
| Calendar / date-picker (`{components.calendar}`) | Schedule Appointment | Radix Popover-based; keyboard-operable natively (arrow keys move between days, `Enter` selects, `Esc` closes). Disabled dates (past, >30 days out, weekends) are visibly distinct and excluded from tab focus, not merely unclickable. |
| Double-entry password fields (`{components.input}` ×2) | Register, Account, Admin edit-password, Admin create-account | Every password entry point in the product is typed twice. A mismatch blocks submission with "Passwords do not match"; only the two password fields clear for retyping, everything else on the form is preserved untouched. |
| Account page form, housed in `{components.form-section}` | Account | Signed-in user edits own first name, last name, and password (double-entry) — not email (FR28). Save routes through the confirm-action popup (`{components.button-primary}` non-destructive Confirm, same as any other non-destructive save). Reuses `{components.input}` for fields and `{components.form-section}` for the card wrapper — same as Login and Register. |

## State Patterns

Walked surface by surface; every empty/cold-load/error/permission state named in the source material is covered.

| State | Surface | Treatment |
|---|---|---|
| Cold load | Schedule Appointment | Booking form renders with all three fields unselected; My Appointments list loads below. A brief "Loading…" text placeholder (muted, no skeleton shimmer) covers the fetch gap — this exact loading treatment isn't specified upstream and is a plain default consistent with the minimal-motion rule. |
| Cold load | My Schedule | Same "Loading…" text placeholder pattern as Schedule Appointment, by symmetry — not called out separately upstream, but there's no reason for the two surfaces to diverge on this. |
| Registration success | Register | Account is created but the user is *not* auto-signed-in — UJ-1 in the PRD narrates registration and sign-in as separate visits ("on a return visit, the customer signs in..."). Landing behavior: redirected to Login with a plain confirmation line above the form (default copy: "Account created. Sign in to continue."), not auto-routed per FR4's post-sign-in rule, since that rule only fires on an actual sign-in event. |
| Account save success | Account | After a successful self-service save, a plain confirmation line appears above the form (default copy: "Changes saved."), matching the no-color-alert convention used elsewhere — the signed-in session continues uninterrupted (FR28), so there's no redirect, just visible confirmation that the save took effect. |
| No barbers available | Schedule Appointment (barber selector) | Selector shows the literal text "No barbers available" in place of options (FR6); date and time fields remain visible but produce nothing bookable until a barber exists. |
| Double-booking race | Schedule Appointment (Submit) | If the chosen slot is taken between page-load and submit: on-screen error ("That time is no longer available. Choose another."), barber/date selections retained, time dropdown re-queries current availability. No appointment is silently created or duplicated (FR10). |
| Self double-booking | Schedule Appointment (Submit) | A signed-in user already holding an appointment at the same date/time with a different barber is blocked the same way, with an equivalent on-screen error (FR9). |
| My Appointments — empty | Schedule Appointment (list) | Plain-text default: "No upcoming appointments." — exact copy not specified upstream, flagged as an on-brand default. |
| Weekend / shop-closed | My Schedule; Schedule Appointment calendar | My Schedule shows no bookable slot grid for a weekend date reached via the arrows; the booking calendar disables weekend dates outright, consistent with the same rule (FR7/FR13). |
| Stale-cancel conflict | My Schedule (Cancel) | Attempting to cancel an appointment already cancelled by someone else (a race, not a bug) returns an error rather than a silent no-op; the view refreshes to the current, accurate state (FR30). |
| Cancellation double-attempt | Schedule Appointment / My Schedule (Cancel) | A second cancel on an already-cancelled appointment is rejected with an error, never a silent success or a crash — cancellation succeeds exactly once (FR30). |
| Login error | Login | Identical generic error for both "email not registered" and "wrong password" — "Invalid email or password." No user enumeration (FR2). |
| Login rate-limited | Login | After repeated failed attempts, an on-screen message (default copy: "Too many attempts. Try again in a few minutes.") replaces the generic invalid-credentials error; the exact attempt threshold and cooldown are deferred to Architecture (NFR1), so this state's *existence* and copy are specified here without a specific number attached. |
| Duplicate email | Register, Admin edit popup, Admin create popup | Submitting an email already tied to another account is rejected with an on-screen error (default copy: "That email is already in use.") rather than a generic failure; the email field is retained for correction, other fields untouched (FR1/FR18/FR19 — email is the account's unique key everywhere it's collected or edited). |
| Signed-out hits a protected surface | Schedule Appointment, My Schedule, Admin Panel, Account | Redirected to Login. After a successful sign-in the user lands per FR4's role rule (customer → Schedule Appointment, barber/admin → My Schedule) — not necessarily back to the page originally requested; no "return to where I was" deep-link behavior is specified upstream, so this spine assumes none exists. |
| Wrong-role direct-URL access | My Schedule (another barber's view), Admin Panel (non-admin) | Rejected server-side the same way as signed-out (FR3/FR14). Since the nav link is never shown to begin with, this only happens via manual URL entry; treatment defaults to redirecting to the user's own default landing page with no visible "blocked" screen — mirroring the product's broader stance that role-gated surfaces are hidden, not flaunted-then-refused. |
| Password mismatch | Register, Account, Admin edit-password, Admin create-account | "Passwords do not match," shown as plain text (`{typography.caption}`, `{colors.text}`) rather than a color-coded alert — a deliberate choice to keep the palette to primary blue + destructive red only, with red staying reserved for destructive actions. Only the two password fields require retyping. |
| Admin Panel — no search results | Admin Panel | Plain-text default: "No accounts match your search." — exact copy not specified upstream, flagged as a default. |
| Admin Panel — before any search | Admin Panel | Empty results area with a muted prompt, default: "Search by name or email to find an account." The zero-query state isn't specified upstream; this is a plausible, on-brand default. |
| Concurrent account-edit conflict | Admin Panel edit popup; Account page (racing an admin edit) | First commit wins; the second gets a conflict error rather than silently overwriting. Default copy: "This account was changed elsewhere. Refresh and try again." (FR41). |
| Admin-driven password change (another account) | Any surface, for the target account | Every active session for that account is terminated immediately — their next action anywhere fails auth and forces re-sign-in (FR35). |
| Self-service password change | Account page | The signed-in user's own current session continues uninterrupted; no forced re-login (FR28). |
| Admin-driven permission change | Target account's other open sessions | Not force-ended. The affected user's next page refresh picks up the new role and its nav/permission consequences (FR35). |
| Barber demotion / account deletion cascade | Admin Panel edit (demote) or delete | Future (not-yet-occurred) appointments for that barber are cancelled and deleted; past appointments are retained in the database as history in both cases, though (per FR24) they're never surfaced in any customer-facing list (FR18/FR40). |

## Interaction Primitives

Fake Barbershop has no command palette or keyboard-shortcut surface — it's a task-focused scheduling tool, not a power-user app. The primitives that matter here are pointer-vs-touch discipline and making the three Radix-backed components (and everything hand-built alongside them) equally operable without a mouse.

- **Pointer/hover (desktop):** hover fires a darker-hue treatment on buttons, nav links, dropdown options, and admin account rows (per `{colors.primary-hover}` / `{colors.destructive-hover}` / `{colors.neutral}`). Hover is additive polish, never the only way to discover that a row or button is interactive.
- **Touch:** no hover-dependent behavior exists. Every action — including ones that reveal a hover affordance on desktop — fires on a single, complete tap. No double-tap-to-activate anywhere, a deliberate rule tracing to a known real-world touch bug the client wants avoided (FR20/FR22/NFR3).
- **Keyboard — Radix-backed surfaces (calendar, all dropdowns/selects, modal/dialog):** full keyboard operability comes from the primitive itself — arrow keys move through calendar days or dropdown options, `Enter`/`Space` selects, `Esc` closes, focus is trapped inside an open modal and returns to the trigger on close. This is close to free engineering-wise; it still needs verifying, not assuming.
- **Keyboard — everything else (buttons, nav links, schedule rows, admin account rows, form layout):** fully custom, so the same standard has to be built deliberately: every interactive element is `Tab`-focusable in visual/reading order, activates on `Enter`/`Space`, and shows a visible focus ring.
- **Popups:** `Esc` or an outside click closes the topmost popup exactly as "Go Back" would — no state is committed, no unsaved edit is lost from the *page* (only the popup's own in-progress edit is discarded). At most one popup is open at once, with the single one-level stacking exception noted in Information Architecture.
- **Forms:** `Enter` inside a single-action form (Login, Register, the booking form) submits it, matching ordinary browser form conventions — not separately specified upstream, treated as a sensible default.

**Banned everywhere:** hover-only affordances with no touch equivalent; double-tap-to-activate; silently overwriting a conflicting edit/cancel/booking instead of surfacing an error; popup stacks deeper than one level; a color-only signal for anything that also needs a text label (error states, booked-vs-open rows).

## Accessibility Floor

- WCAG 2.2 AA baseline across the entire responsive surface — an explicit "normal, consumer-grade rigor" stakes decision, not a stretch goal.
- The three Radix UI primitives — calendar/date-picker, dropdown/select, modal/dialog — get correct keyboard nav, focus-trapping, and ARIA semantics essentially for free from the library. This is *why* those three specific surfaces were chosen for Radix in the first place: they're the highest-risk spots for accessibility bugs if hand-rolled. Everything else — buttons, nav, forms, schedule rows, admin rows — is fully custom and needs the identical rigor applied deliberately; nothing free carries over to those.
- Role-based nav hiding (FR3) must be a real removal from the DOM and tab order, not a `display:none`-only visual hide with the link still reachable by `Tab` — a screen-reader user on the wrong role should never even discover Admin Panel or My Schedule exist.
- No state relies on color alone: booked vs. open schedule rows are distinguished by text content (customer name + Cancel vs. "Available"), not just row tint; the password-mismatch state is plain text by design (see State Patterns for why no dedicated error color exists).
- `{colors.destructive}` on white computes to ≈5.06:1, clearing the 4.5:1 AA threshold for normal-size text (button labels use 14px/600). The original candidate shade fell just short at ≈4.38:1; the client chose to darken the fill rather than accept the shortfall — see DESIGN.md's Colors section for the resolved value.
- Every hover-revealed affordance has a keyboard equivalent (`Tab` + visible focus) and a touch equivalent (single tap) — accessibility and the touch constraint are the same requirement here, not two separate ones.
- Focus rings must be visible at AA contrast against `{colors.background}` on every custom-built interactive element, matching the standard the Radix primitives already meet.

## Responsive & Platform

| Breakpoint | Behavior |
|---|---|
| `≥ 1024px` (desktop) | Nav bar shows all links inline. Schedule Appointment's form and My Appointments list, and My Schedule's date header and slot list, sit within the `{spacing.content-max-width}` (1120px) column. Hover states active. |
| `640–1023px` (tablet) | Content gutters narrow toward `{spacing.gutter-mobile}`. Nav-collapse pattern below this width is not specified upstream — DESIGN.md flags the breakpoint values themselves as a placeholder default — so this spine proposes the plain, low-risk default: links remain inline as long as they fit, collapsing to a menu button only once they'd wrap, using the same custom-built styling as the rest of the nav. |
| `< 640px` (mobile) | Single-column stacking throughout: booking form fields stack vertically above My Appointments; My Schedule's date header stacks above its slot list; Admin Panel's search bar sits above a full-width stack of account rows. All hover-only affordances are absent; every action is a single tap. |

Fake Barbershop is responsive web only — mobile and desktop viewports, no native app, no dark mode (light-only, per DESIGN.md). The product must work end-to-end on a phone, not just render without breaking: booking, cancelling, and admin account management are all expected to be fully usable on a touch device, not read-only.

## Inspiration & Anti-patterns

The source material doesn't reference outside products as direct inspiration — the visual and behavioral system here was originated from scratch, not lifted from a named competitor. The one anti-pattern explicitly on record is worth stating on its own:

- **Rejected — hover-reveal-only actions and double-tap-to-activate on touch.** This traces to a specific, named real-world bug the client has encountered elsewhere and explicitly wants avoided (FR20/FR22/NFR3): an action that only becomes tappable after a first "reveal" tap, forcing a confusing double-tap. Every action in Fake Barbershop is a single, complete tap on touch, full stop.
- **Rejected — celebratory confirmation UI.** No confetti, no exclamation points, no "🎉" on the post-booking confirmation screen — it states the fact of the booking and stops, consistent with the clean/professional voice register.
- **Rejected — destructive-red used for anything other than a destructive action.** No warning banners, validation errors, or emphasis text ever borrow `{colors.destructive}` — that color's entire meaning in this system is "this button cancels or deletes something," and diluting it for a validation message would break that signal.

## Key Flows

### Flow 1 — Jack books before a haircut he needs the next day

1. Jack is signed out, browsing Home.
2. He clicks "Sign In" in the nav, reaching Login; enters email and password.
3. Sign-in succeeds. Per FR4, he lands directly on Schedule Appointment — never Home.
4. He selects barber Manny from the barber-select dropdown.
5. He opens the calendar and picks tomorrow, July 24 (weekends and dates past the 30-day window aren't selectable).
6. He opens the time dropdown, which lists only Manny's open slots for July 24, and selects 11:00 AM.
7. He clicks Submit. A brief load follows.
8. **Climax:** the booking form is replaced entirely by a confirmation screen reading "Appointment booked with Manny at 11:00 AM on July 24." Jack closes the tab, certain the booking exists — no phone call, no waiting on hold, nothing left to double-check.

*Failure:* if that 11:00 AM slot is claimed by someone else in the gap between Jack's page load and his Submit, he gets an on-screen error instead of a confirmation; his barber and date selections are retained and the time dropdown updates to reflect current availability, so he can pick another slot immediately. No appointment is silently created or duplicated on either side of the race.

### Flow 2 — Manny checks his day

1. Manny signs in and, per FR4, lands directly on his own My Schedule — never Home.
2. The view defaults to today, listing every 30-minute slot from 9:00 AM to 4:30 PM.
3. Most slots read "Available"; 11:00 AM shows "Jack [lastname]" — Jack's booking from the night before.
4. Mid-afternoon, between cuts, Manny checks his schedule again without being prompted to.
5. A new booking has appeared at 2:30 PM, made by a different customer online while Manny was mid-haircut and never touched the app.
6. **Climax:** Manny never got a phone call or a separate list to reconcile — the one schedule view he already had open simply reflects the new booking, exactly where it belongs in the day.

*Failure:* Manny tries to cancel an appointment a customer cancelled seconds earlier from their own side — a stale page, not a mistake. He gets a conflict error instead of a silent double-cancel, and the view refreshes to the current, accurate state.

### Flow 3 — The Owner manages accounts

1. The Owner signs in and, per FR4, lands on the same schedule view a barber sees, plus a Select Barber dropdown defaulted to the first barber.
2. They switch the dropdown to Manny's day to spot-check his schedule.
3. While reviewing accounts, they notice a barber's email was mistyped at account creation.
4. They go to Admin Panel, search by name, and find the account among the matching rows.
5. Clicking the account row opens a popup with editable fields (email, first name, last name, permission level, password).
6. They correct the email and click Save, which opens the confirm-action popup: "Go Back" (neutral) and "Confirm" (`{colors.primary}` blue — non-destructive, since this is a save, not a deletion).
7. They click Confirm.
8. **Climax:** back on the Admin Panel, the corrected email is already reflected in the account row — the identical popup-edit-confirm rhythm handles a quick fix like this one or something more drastic (an account deletion follows the exact same popup, just with a destructive-red Confirm instead of blue).

*Failure:* the Owner has two browser tabs open and edits the same account in both. The second submit gets a conflict error rather than silently overwriting the first commit — the edit that landed first stands, and the second attempt has to be retried against current data.

### Flow 4 — Signing out (any role)

UJ-4 is deliberately not narrated as a full flow — it's a single action with no branching or climax beat worth dramatizing, so it's covered mechanically instead: any signed-in user (Jack, Manny, or the Owner) opens the profile-icon dropdown and selects Logout. The session ends server-side across every tab and device that account was signed into (FR23), not just the current one — the next action anywhere with that session fails auth and requires signing in again.
