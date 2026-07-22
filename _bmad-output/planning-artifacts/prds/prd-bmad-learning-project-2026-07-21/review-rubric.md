# PRD Quality Review — Barbershop Appointment Scheduler (prd-bmad-learning-project-2026-07-21)

## Overall verdict

This PRD is unusually well-executed for its scale: FRs carry testable consequences, the Success Metrics table pairs every metric with an honest counter-metric, and technical leanings are correctly routed to the addendum with hedged language rather than smuggled in as decisions. The two real risks are strategic, not mechanical — the Success Metrics validate that the software behaves correctly, not that the stated problem (phone-booking friction) actually gets solved — and structural for downstream work, since there's no Glossary and two pairs of near-identical UI names ("Accounts" tab vs. "Account" page; "Schedule Appointment page" vs. "booking page") are left to drift. Neither is fatal, but both should be resolved before this feeds UX and Architecture.

## Decision-readiness — adequate

The Goals & Success Metrics table (lines 18–28) is the strongest evidence of real decision-readiness in the document: every metric is paired with a counter-metric that names what could be gamed or missed (e.g., "Gate holds at the request layer, not just hidden UI" against the auth metric; "Suite is capable of catching a real regression, not rubber-stamping every change" against the CI metric). The addendum correctly defers the JWT-vs-cookie call to Architecture with explicit hedging ("leaning, not locked") rather than presenting it as settled. The Non-Goals section states real trade-offs, not just omissions — e.g. "Self-service password reset flow (admin can directly change any account's password, covering this need without a reset-link/email mechanism)" names what was given up and why.

Weighed against that: there are zero `[NOTE FOR PM]` callouts and only one Open Item (guest booking) in a PRD the user explicitly asked to hold to full rigor. That may be an accurate reflection of a genuinely well-resolved PRD, but a couple of real tensions are handled by omission rather than an explicit callout — e.g., FR7's "no forward-looking limit" on booking dates is stated as a decision with no visible trade-off discussion (unlimited future booking has real UX and calendar-rendering implications that aren't acknowledged one way or the other).

### Findings
- **low** Decision scaffolding relies on addendum + Open Items instead of `[NOTE FOR PM]` tags (whole doc) — Functionally most tensions are surfaced, but the rubric's expected callout format is absent everywhere, and a couple of real decisions (FR7's unbounded forward booking window) have no visible trade-off note at all. *Fix:* add a one-line `[NOTE FOR PM]` at FR7 confirming the unlimited-forward-booking call was deliberate, or add a bound.

## Substance over theater — strong

No persona theater, no innovation/differentiation section manufactured for the template, no vision-statement swap-in-any-PRD language — the Overview ties directly to a named problem ("give customers a way to book a haircut without calling and waiting on hold"). NFRs are mostly specific and product-scoped rather than boilerplate: NFR2 names the exact concurrency failure mode, NFR4 requires a real (not mocked) DB in tests, NFR7 explicitly bounds the deployment target to local-only. The one exception is noted under Done-ness clarity below (NFR6) rather than here, since it reads as a soft engineering value rather than manufactured theater.

## Strategic coherence — adequate

The thesis (reduce phone-call booking friction; give each role exactly the visibility it needs) is stated once and the FR groupings follow it cleanly — Booking, Barber Dashboard, and Admin Dashboard map directly onto the three roles named in the Overview, and the Non-Goals de-scoping (payment, notifications, multi-location) matches a "problem-solving MVP" scope logic rather than an "easy first" backlog order.

The gap: every row in the Goals & Success Metrics table (lines 18–28) measures whether the software behaves correctly — auth gating passes, a booking write lands in SQLite, double-booking is rejected, CI stays green, layout renders, sessions persist. None of them measure whether the thesis itself holds — whether customers actually book online instead of calling, whether the shop's phone volume drops, whether barbers actually use the schedule view day to day. For a PRD explicitly treated as real-stakes rather than a hobby exercise, this means the document has no way to later say "the problem got solved," only "the code passed its tests." This is close to the rubric's own red-flag pattern for this dimension (metrics that measure activity/correctness rather than the thesis).

### Findings
- **high** Success Metrics validate implementation correctness, not the stated problem thesis (§ Goals & Success Metrics, all 7 rows) — Every metric is a QA/acceptance gate (auth gating, DB write, double-booking rejection, CI, code navigability, responsive layout, session persistence); none measure adoption or the phone-call-friction problem the Overview names as the reason this exists. *Fix:* add at least one outcome-oriented metric with an honest counter-metric — e.g. "N appointments booked online in the first two weeks post-launch" against "not cannibalized entirely by walk-ins/no real usage" — or explicitly note that business-outcome metrics are out of scope until post-launch and say why.

## Done-ness clarity — strong

Most FRs carry a testable consequence: FR10's double-booking guard is backed by a concrete acceptance test in the SM table ("Two near-simultaneous submissions for the same slot resolve to exactly one booking"); FR14 ("never another barber's") and FR3 (route-gating + hidden nav tab) are both binary and verifiable; FR22 pairs "renders cleanly" with a concrete negative ("no broken/overflowing layouts"). This is a genuinely unusual level of rigor for FRs at this scale.

Two soft spots:

### Findings
- **low** NFR6 is unfalsifiable ("a reader can locate where any given behavior lives without hunting," "no premature abstraction, no dead scaffolding") — This reads exactly like the boilerplate the rubric asks to flag ("system handles X gracefully"), just phrased more specifically. There's no way to write a test or CI check against it. *Fix:* replace with a concrete proxy (e.g., a file/function size ceiling, "one responsibility per controller/service") or move it to a manual code-review checklist rather than an NFR alongside testable ones.
- **low** FR10's "rejected with a clear error" leaves "clear" undefined (§ Booking, FR10) — Largely mitigated by the SM table's concrete acceptance test on the same behavior, but the FR text itself doesn't specify what the error must communicate to the user. *Fix:* state the minimum content (e.g., "error names the slot as no longer available and reloads current availability").

## Scope honesty — strong

The Out of Scope / Non-Goals section (lines 96–104) does real work — each bullet explains the trade-off rather than just naming the omission (e.g., "Guest booking (unauthenticated booking) — possible same-day addition, not committed" and "An actual deploy target or public hosting... CI keeps the codebase always-deployable, nothing is actually deployed anywhere"). The addendum correctly routes uncertain technical calls (session mechanism) out of the PRD with explicit hedging rather than either locking them prematurely or leaving them silently implied. Open-items density (one item) is appropriately low for a tightly-scoped solo build rather than a sign of suppressed tensions — the one item present (guest booking) is genuinely undecided, not a rhetorical question with its answer nearby.

## Downstream usability — thin

This PRD is chain-top (feeds UX → Architecture → Epics/Stories), so this dimension carries more weight than it would for a standalone document. FR/UJ numbering is actually solid — all of FR1–FR30 are present exactly once despite being grouped thematically rather than sequentially, and UJ-1–UJ-4 are contiguous. But two structural gaps will cost the next workflow real time:

- No Glossary section exists anywhere in the document. Domain nouns are mostly used consistently, but not uniformly: the admin-facing "**Accounts** tab" (FR16 — "A separate Accounts tab hosts account management") and the personal "**Account** page" (FR28 — "On a new Account page, a signed-in user can edit their own..."; FR29 — "Account (link to the Account page)") are two different surfaces with near-identical names. A UX designer or architect skimming FRs out of context could easily conflate them.
- The same booking surface is named three ways: "Schedule Appointment page" (FR5, FR6, FR9...), "Schedule Appointment nav tab" (FR3), and "the shared booking page" (UJ-2). Likely the same object, but nothing states that explicitly.
- The Success Metrics table rows (lines 18–28) are unlabeled — no SM1...SM7 — so nothing downstream can cite a specific metric by ID; every cross-reference has to quote text.

### Findings
- **medium** No Glossary + naming collision between "Accounts" (admin tab, FR16) and "Account" (personal page, FR28/FR29) — For a chain-top PRD, this is exactly the kind of drift that becomes a UX/Architecture rework later. *Fix:* add a short Glossary defining each named UI surface once; consider renaming the admin tab (e.g., "Staff Accounts" or "Manage Accounts") to remove the collision.
- **medium** Success Metrics table rows carry no IDs (§ Goals & Success Metrics) — Downstream Epics/Architecture work can't reference "SM3," only quote the row's prose. *Fix:* number the rows SM1–SM7.

## Shape fit — adequate

For a three-role booking tool this size, the shape is right: UJs are load-bearing (each ties directly to a distinct role's FR block) rather than decorative, and the PRD correctly skips competitive-differentiation and demographic-persona sections that would be theater at this scale. The addendum split (technical leanings kept out of the capability-level PRD) is exactly the right move for a chain-top document. Where it under-delivers on "full rigor" is precisely the mechanical scaffolding covered above and below — Glossary, SM IDs, assumption tagging — which is where a hobby-scaled treatment would also normally cut corners. Substantively the PRD clears the bar; structurally it's missing a few of the connective pieces that make downstream extraction cheap.

## Mechanical notes

- **Glossary drift**: "Accounts" (admin tab, FR16) vs. "Account" (personal page, FR28/FR29); "Schedule Appointment page" (FR5, FR6, FR9) vs. "Schedule Appointment nav tab" (FR3) vs. "the shared booking page" (UJ-2) — likely the same referents in both cases, never stated as such. See Downstream usability above for the fix.
- **ID continuity**: Strong. FR1–FR30 all present exactly once despite non-sequential thematic grouping (e.g., Authentication & Accounts holds FR1–4, FR23, FR28, FR29). UJ-1–UJ-4 contiguous. No gaps or duplicates found.
- **Assumptions Index roundtrip**: No `[ASSUMPTION]` tags and no Assumptions Index appear anywhere in the PRD. This isn't obviously a defect — the addendum absorbs the PRD's genuine uncertainties (session mechanism) with explicit "leaning, not locked" language instead — but it means there's no formal record of what was inferred vs. confirmed during discovery, which the rubric treats as a standard chain-top artifact.
- **UJ protagonist naming**: Protagonists are role-generic ("a new customer," "a barber," "an admin") rather than named individuals. Given this is a three-fixed-role internal/customer tool rather than a multi-persona consumer product, this is likely the right call for the shape — flagging only as an observation, not a fix-needed item.
- **Required sections**: Overview, Goals & Success Metrics, User Journeys, Functional Requirements, NFRs, Out of Scope, Deliverables & Timeline, and Open Items are all present and appropriately weighted for a chain-top capability-level PRD at this stakes level. No section reads as missing for the agreed shape other than the Glossary noted above.
