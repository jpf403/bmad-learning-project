# Spine Pair Review — bmad-learning-project

## Overall verdict

DESIGN.md and EXPERIENCE.md form a strong, largely self-sufficient contract: every `sources` path resolves, every `{colors.*}`/`{typography.*}`/`{rounded.*}`/`{spacing.*}`/`{components.*}` reference in both files resolves to a defined frontmatter token, FR/UJ citations check out against prd.md, canonical section order is followed exactly in both files, and state/flow coverage is unusually thorough for a project this size (22 rows in State Patterns, 3 fully-narrated Key Flows). No reference is broken and no load-bearing decision is silently missing. The real gaps cluster in a handful of secondary components that never got a dedicated visual row (My Schedule's date-header arrows, the My Appointments row shell, the admin edit/create popup) and two functional states that fall between the documented cases (what happens right after Register succeeds; what confirms a self-service Account save landed). None of these block a downstream consumer from building the three Key Flows end-to-end, but each would force an implementer to invent an unreviewed default the way the doc itself already does explicitly elsewhere — these should get the same explicit-default treatment rather than silence.

## 1. Flow coverage — strong

Sources frontmatter resolves to prd.md, which names 4 UJs (UJ-1 customer books, UJ-2 barber views schedule, UJ-3 admin manages accounts/schedules, UJ-4 sign out). EXPERIENCE.md's Key Flows section carries three fully-formed flows: Flow 1 (Jack, customer booking, 8 numbered steps, bolded climax, explicit failure path), Flow 2 (Manny, barber, 6 steps, climax, failure path), Flow 3 (The Owner, admin, 8 steps, climax, failure path) — these map cleanly to UJ-1/UJ-2/UJ-3 respectively, each with a named protagonist, numbered steps, climax beat, and failure path exactly per spec.

### Findings
- **low** UJ-4 (sign out) has no dedicated Key Flow — it's only covered behaviorally, one sentence, inside the Component Patterns table's "Profile icon dropdown" row (EXPERIENCE.md, Component Patterns). *Fix:* either explicitly note in Key Flows that UJ-4 is intentionally omitted as a narrative flow (it's a single action with no branching climax), or fold a two-line "Flow 4 — Sign out" stub in for mechanical completeness.

## 2. Token completeness — strong

Every frontmatter token (11 color tokens, 8 typography roles, 6 rounded scale steps, 14 spacing entries, 14 named components) is defined with concrete values; all colors carry hex (no light/dark split needed — light-only is an explicit locked decision). Every `{path.to.token}` reference found in both files' prose and component objects resolves to a token actually declared in DESIGN.md's frontmatter — spot-checked `{components.nav-bar.link-foreground-active}`, `{spacing.content-max-width}`, `{colors.primary-hover}`, `{components.select-dropdown-admin-barber}`, and all typography/rounded refs, all resolve cleanly. Contrast is explicitly computed and stated for the two narrow-margin combinations (`{colors.primary}` ≈4.79:1, `{colors.destructive}` ≈5.06:1, both hover states), matching the design-md-spec's expectation that color tokens carry contrast where it's load-bearing.

### Findings
- **medium** `{colors.text-muted}` (`#5B7480`) is used across placeholder text, inactive nav links, and secondary/muted copy throughout the product (DESIGN.md Colors, Components) but never gets a stated contrast ratio, unlike primary/destructive. Computed against `{colors.background}` (white) it lands at ≈4.93:1 — clears AA's 4.5:1 for normal text, but by a narrower margin than the doc's own precedent (primary/destructive) treats as worth calling out explicitly. (DESIGN.md frontmatter `text-muted: '#5B7480'`; Colors section). *Fix:* add one sentence stating the computed ratio, the same way primary/destructive got one, so a future re-tint of this token doesn't silently drop below AA unnoticed.

## 3. Component coverage — adequate

Every component named in DESIGN.md's `components` frontmatter and Components body section has a matching behavioral row in EXPERIENCE.md's Component Patterns table with real rules (not one-word descriptions) for the primary interactive set: buttons (primary/secondary/destructive), nav bar, calendar, select-dropdowns (customer + admin-barber variant), confirm-action popup, admin account row, schedule rows (open/booked), confirmation screen, and double-entry password fields. A few secondary components named and behaviorally specified in EXPERIENCE.md never got a matching dedicated visual row in DESIGN.md.

### Findings
- **medium** "Date header + arrows" (EXPERIENCE.md Component Patterns, My Schedule surface) has behavioral rules (day-step arrows, defaults to today, weekend handling) but the arrow controls themselves have zero visual spec anywhere in DESIGN.md — no icon reference, color, size, or hover/active token. The header text role is inferable from Typography's "e.g. ... the date header" aside, but the arrows are wholly unspecified. *Fix:* add a small `date-nav-arrow` component entry (or fold into `nav-bar`/a new token) specifying color, size, and hover behavior.
- **medium** "My Appointments list" (EXPERIENCE.md Component Patterns, Schedule Appointment surface) specifies behavior (own bookings only, Finished tag, Cancel button) but its row shell has no DESIGN.md visual component — `{components.schedule-row-open}`/`{components.schedule-row-booked}` are explicitly scoped to "My Schedule" only in both files, so it's unclear whether My Appointments rows reuse that visual treatment or need their own. *Fix:* either explicitly state My Appointments rows reuse `{components.schedule-row-booked}`'s visual treatment, or add a dedicated row.
- **low** "Admin account-edit popup" and "Admin account-create popup" (EXPERIENCE.md Component Patterns) have detailed behavioral rules (field lists, permission-dropdown constraints per role) but no dedicated DESIGN.md visual row — only inferable by composing `{components.modal}` (frontmatter) + `{components.input}` + `{components.select-dropdown}` + button tokens. Workable by composition, but thinner than the rest of the doc's per-component treatment. *Fix:* a short DESIGN.md row naming the field layout/spacing inside the modal would remove the inference step.

## 4. State coverage — adequate

Walked every IA surface (Home, Login, Register, Schedule Appointment, My Schedule, Admin Panel, Account, About, Confirmation screen, Confirm-action popup). Coverage is unusually thorough: cold-load, no-barbers, double-booking race, self double-booking, empty/finished appointment states, weekend-closed, stale-cancel conflict, cancel double-attempt, login error, login rate-limit, duplicate email, signed-out redirect, wrong-role URL access, password mismatch, admin search empty/pre-search, concurrent-edit conflict, and three session-consequence states (admin password change, self password change, permission change) are all named with copy and field-retention behavior. Two states fall through the surface walk.

### Findings
- **medium** No state or flow specifies what happens immediately after a successful Register (FR1) — auto-signed-in and routed per FR4, or left to sign in manually via Login. UJ-1's prose ("On a return visit, the customer signs in...") implies these are separate events, but neither the IA table nor State Patterns states the post-Register destination. (EXPERIENCE.md, Information Architecture / State Patterns). *Fix:* add a "Registration success" row to State Patterns (or IA) stating the landing behavior, consistent with how FR4's post-Login landing is already spelled out.
- **medium** No state specifies the success feedback for a self-service Account edit (FR28). Flow 3's climax explicitly covers the *admin*-edit case ("the corrected email is already reflected in the account row"), but the analogous self-service Account page save has no equivalent confirmation described — a user could reasonably wonder whether the save took effect. (EXPERIENCE.md, State Patterns / Component Patterns — Account surface has no row at all in Component Patterns). *Fix:* add a one-line state describing what the Account page shows after a successful save (e.g., a plain confirmation message, matching the no-color-alert convention already used for validation states).
- **low** My Schedule's cold-load state isn't specified, unlike Schedule Appointment's explicit "Loading…" placeholder row. Likely the same treatment applies by symmetry, but it isn't stated. *Fix:* either state that My Schedule reuses the same "Loading…" placeholder pattern, or add its own row.

## 5. Visual reference coverage — strong

No `mockups/`, `wireframes/`, or `imports/` directory exists yet under this UX folder — confirmed via directory listing. This is expected at this stage (the project hasn't reached mockup promotion) and is treated as such, not a defect. The one file under `.working/` (`color-themes-1.html`) is cited once, in a YAML comment, as provenance for the picked color direction — it resolves (file exists) and isn't presented as a spine-level composition reference the way Drift's example cites `mockups/*.html`, so there's no ambiguity about what wins on conflict. No orphans, no unspecific references.

## 6. Bloat & overspecification — adequate

DESIGN.md's editorial voice (Brand & Style, Colors narrative) is appropriate per spec allowance and matches the reference example's register. Neither file restates PRD requirement text wholesale — both cite FR/UJ numbers inline rather than reproducing them, which is the efficient pattern the reference examples use. No pixel specs duplicate/conflict with tokens; raw dimensions (nav-bar height, spacing scale) live in frontmatter where they belong. Key Flows' narrative color (e.g. Flow 1's "no phone call, no waiting on hold, nothing left to double-check") matches the reference example's expected voice for that specific section.

### Findings
- **low** EXPERIENCE.md's Foundation section drifts into editorial/persuasive register rather than staying behavioral: "Every state pattern below that looks like friction ... exists because skipping it would trade a correctness guarantee for a smoother-looking demo, which this spine treats as the wrong trade every time" is argumentative narration, not a spec statement — a register the Drift EXPERIENCE.md example avoids even in its own Foundation section. (EXPERIENCE.md, Foundation, final paragraph). *Fix:* trim to the factual claim ("every friction state below reflects a real correctness check, not a shortcut") without the persuasive framing.

## 7. Inheritance discipline — strong

`sources` frontmatter is identical in both files and both paths resolve (prd.md, addendum.md both exist). FR/UJ citations spot-checked against prd.md (FR3, FR4, FR9, FR29, FR31/34, FR41, SM8, NFR1) are accurate paraphrases, not corrupted or misattributed. No formal Glossary section exists in either file, but none is required by the shape spec and terminology (barber/customer/admin, surface names) is used consistently across both spines and the PRD — not a gap. Every EXPERIENCE.md token reference resolves to a DESIGN.md token by name (see §2). Component names match across both files' sections for the primary component set, with one exception:

### Findings
- **low** "Home hero" (DESIGN.md Components) vs. "Home hero CTA" (EXPERIENCE.md Component Patterns) — same functional area named slightly differently across the two files. Low-severity since the behavioral row only ever needed to cover the CTA button (the graphic half isn't interactive), but the name drift is a minor mechanical inconsistency. *Fix:* rename one to match the other (e.g., DESIGN.md's row could stay "Home hero" while noting the CTA sub-element uses `{components.button-primary}`, which it already does).

## 8. Shape fit — strong

DESIGN.md's body sections appear in exact canonical order: Brand & Style → Colors → Typography → Layout & Spacing → Elevation & Depth → Shapes → Components → Do's and Don'ts. EXPERIENCE.md carries all required defaults — Foundation, Information Architecture, Voice and Tone, Component Patterns, State Patterns, Interaction Primitives, Accessibility Floor, Key Flows — plus two required-when-applicable sections (Responsive & Platform, Inspiration & Anti-patterns), in the same order the reference example uses. No invented sections; nothing dropped without it being defensible (no offline states — appropriate, since nothing in the PRD/addendum implies an offline mode for a locally-run app; no dark-mode section — explicitly a locked light-only decision).

### Findings
None.

## Mechanical notes

- Frontmatter is complete and consistent between the two files: both declare `name`, `status`, `sources`, `updated` with identical values.
- No Mermaid diagrams appear in either file — nothing to check syntactically.
- FR29 labels the signed-out nav button "Login" while EXPERIENCE.md's IA table calls it "Sign In" — already adjudicated as a cosmetic, non-behavioral difference in `reconcile-prd.md` (not re-flagged here as a fresh finding).
- Flow 3's protagonist is named "The Owner" (a role) rather than a personal name, unlike Flow 1's "Jack" and Flow 2's "Manny" — defensible, since the admin is a single seeded, unnamed account (FR31/FR34), but it's a minor stylistic inconsistency worth noting.
- The three gaps previously found by `reconcile-prd.md` (duplicate-email state, login rate-limit state, admin create-vs-edit permission-field contradiction) are confirmed fixed in the current EXPERIENCE.md — all three are present and correctly scoped in the current text.
