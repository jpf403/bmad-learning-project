# Input Reconciliation: Brief (+ Addendum) vs. PRD (+ Addendum)

Scope of this document: verify nothing from the brief was silently dropped, weakened, or
contradicted by the PRD, **excluding** the five known/logged divergences the requester already
tracks (outside-story framing removal, mobile responsiveness reversal, guest booking deferral,
password-reset-via-admin-edit substitution, and technical/schema detail deferral to Architecture).

---

## 1. Business Rules (brief addendum) vs PRD

| Brief addendum rule | PRD treatment | Verdict |
|---|---|---|
| Slots 9:00 AM–5:00 PM, fixed 30-min increments, 4:30 PM last bookable | FR8 ("fixed 9:00 AM–4:30 PM, 30-minute increments"), UJ-2 ("lists every fixed time slot from 9:00 AM to 4:30 PM in 30-minute increments") | Preserved. Minor: PRD never restates "business day ends at 5:00 PM" as the reason 4:30 is last-bookable — implicit only. Cosmetic, not a rule change. |
| Booking-window cap considered and rejected; final = any future date allowed, only past dates disabled/darkened | FR7 ("past dates disabled, no forward-looking limit") | Preserved. "Darkened" (visual treatment) dropped, but that's a UI-styling detail properly left for UX, not a business-rule loss. |
| Date/time UI: hybrid of calendar widget (date) + dropdown (times, filtered to that day's open slots) | FR7 (calendar widget) + FR8 (dropdown of open slots) | Preserved exactly. |

**No gaps in this category.** All three explicitly-called-out business rules made it into the PRD intact.

---

## 2. Success Criteria (brief) vs Goals & Success Metrics / NFRs (PRD)

Walked each brief Success Criteria bullet against the PRD:

- Auth works end-to-end, gates role pages → PRD Goals row 1 + NFR1 + FR3. Preserved.
- Three roles structurally distinct → PRD Goals row 1, User Journeys, FR11-19. Preserved.
- Real DB interaction demonstrably live (booking writes to SQLite, write reflected back) → PRD Goals row 2. Preserved, near-verbatim.
- Booking-conflict logic holds (double-booking rejected, taken slots don't appear) → PRD Goals row 3 + FR10. Preserved.
- DORA principles visible (CI on every push, always-deployable, live push-and-watch-green moment) → NFR5 preserves the CI/always-deployable mechanics; the "live push during the walkthrough" theatrical moment is dropped — but that's squarely the boss-demo/walkthrough framing already logged as a known, deliberate divergence.
- Codebase maintainable or a reader can find where behavior lives → PRD Goals row 5 + NFR6, near-verbatim.
- Written process reflection exists → deliberately dropped per known divergence (outside-story deliverable).

**No new gaps here** — the only omission (live walkthrough-push moment) is subsumed by the already-logged outside-story stripping.

---

## 3. Scope (brief) vs PRD Functional Requirements / Out of Scope

In-scope brief items all trace to PRD FRs:
- Homepage w/ hover interaction → FR20 (verbatim: "hero, tagline, CTA, and at least one hover interaction").
- About page → FR21.
- Shared booking page for any logged-in role → FR5.
- Hashed-password auth, 3 roles → FR1-3, NFR1.
- Role dashboards (barber own-schedule, admin all-schedules + account mgmt) → FR11-19.
- SQLite booking w/ conflict validation, fixed hours/increments → FR6-10.
- Automated test suite (DB CRUD, auth, role permissions) in CI → NFR4, NFR5.
- Live-demo push moment → dropped, but again subsumed by known outside-story stripping.
- Process reflection doc → dropped per known divergence.

Out-of-scope list: brief's six items (payment, email/SMS, password reset, multi-location, mobile,
deploy target) all appear in the PRD's Out of Scope section, with mobile correctly flagged as
reversed and password reset correctly flagged as covered differently (both already logged).
PRD adds two new out-of-scope items not in the brief (guest booking — already logged as discussed/deferred;
audit trail of admin edits — new, but this is the PRD *adding* a non-goal, not dropping brief content,
so it isn't in scope for this reconciliation).

**No new gaps here.**

---

## 4. "Who This Serves" (brief, in-story) vs PRD Overview

| Brief (in-story) | PRD Overview | Verdict |
|---|---|---|
| Customers: book without calling/waiting on hold, real current availability, a slot that's actually free | "Customers want real, current availability and a time slot that's actually free." | Preserved, near-verbatim. |
| Barbers: see own day of scheduled appointments without digging through everyone else's | "Barbers want their own schedule at a glance." + FR14 ("never another barber's") | Preserved; the "without digging through everyone else's" nuance survives via FR14 even though the Overview sentence itself is shorter. |
| Shop admin: oversight across all barbers, easy control over staff access, without touching the DB directly | "The shop admin wants oversight across all barbers plus easy control over who has staff access — without anyone touching the database directly." | Preserved, near-verbatim (this sentence is actually in the PRD's Goals paragraph, right above Overview's "Who this serves" subsection). |

**No gaps here** — directionally and substantively consistent, as expected since only the outside-story half of this section was meant to be dropped.

---

## 5. Testing Tooling / Specific Test Cases (brief addendum) vs PRD NFRs + PRD Addendum

Brief addendum "Testing Notes" has two parts:

**(a) Specific test cases called out:**
- Booking an already-taken slot rejected with clear error → PRD Goals row 3 + FR10. Preserved.
- Already-booked slots filtered out of the available-times picker → PRD FR8. Preserved.

**(b) Testing tooling (explicitly labeled "informational starting points, not locked decisions"):**
xUnit + WebApplicationFactory (.NET side), Vitest/Jest + React Testing Library (frontend), optional
Playwright (E2E).

→ **GAP.** These tooling leanings do not appear anywhere in the PRD body (understandable — they're
explicitly non-binding) **but they also don't appear in the PRD's addendum.md**, which is exactly the
document the reconciliation brief says exists to carry forward "technical/schema details ... deferred
to the Architecture phase." The PRD addendum currently carries forward only the Auth Session Mechanism
leaning (PBKDF2/bcrypt/Argon2id, JWT-vs-cookie). The parallel testing-tooling leaning from the brief's
addendum was silently dropped rather than being carried into the PRD addendum alongside the auth
leaning — inconsistent handling of two pieces of information that were logged the same way in the
source brief.

---

## 6. Deliverables & Timeline (brief) vs PRD

| Brief | PRD | Verdict |
|---|---|---|
| "Solo build, no other contributors." | "Solo build, no other contributors." | Preserved verbatim. |
| "No hard deadline from the boss." | *(absent)* | Dropped, but this is boss/outside-story framing — covered by the known divergence. |
| "Soft personal target: finished by Friday." | "Soft personal target: finished by Friday, 2026-07-31." | Preserved, now dated. |
| "Realistic outer bound if it runs long: roughly a week and a half of workdays." | *(absent)* | **GAP.** This is a scheduling contingency, not boss-framing — it never mentions the boss or the demo, it's just "if it runs long." The PRD keeps the soft Friday target but drops the fallback/outer-bound estimate entirely, leaving no documented contingency if Friday slips. |

---

## Summary of Findings Beyond the Known/Logged Divergences

1. **Testing-tooling leanings dropped, not carried to Architecture.** The brief addendum's testing tooling section (xUnit + WebApplicationFactory; Vitest/Jest + React Testing Library; optional Playwright) is absent from both the PRD body and the PRD addendum, even though the PRD addendum exists precisely to carry forward this kind of Architecture-facing leaning (as it did for the auth session mechanism). This is an inconsistent, apparently unintentional drop.
2. **Timeline contingency dropped.** The brief's fallback estimate ("realistic outer bound ... roughly a week and a half of workdays" if the Friday target runs long) does not appear anywhere in the PRD's Deliverables & Timeline section, which now states only the hard-dated soft target with no documented fallback window. This detail is independent of the boss/outside-story framing that was deliberately stripped elsewhere.

All business rules (slot increments, booking-window-cap decision, hybrid calendar+dropdown UI),
Success Criteria/Scope mapping, and the in-story "Who This Serves" content were checked in detail
and found faithfully carried into the PRD with no silent drops or contradictions.

Noted but explicitly out of scope for this reconciliation (additive, not dropped): the PRD introduces
a cancellation feature (FR24-27, FR30) and an Account self-edit page (FR28-29) that have no
counterpart in the brief. These are scope *additions*, not drops of brief content, so they don't
constitute "gaps" under this task's definition — flagged here only for completeness/awareness.
