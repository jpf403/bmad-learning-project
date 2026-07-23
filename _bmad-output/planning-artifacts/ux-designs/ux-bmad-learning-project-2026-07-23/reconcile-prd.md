---
title: UX Spine Reconciliation — DESIGN.md + EXPERIENCE.md vs PRD
source_prd: ../../prds/prd-bmad-learning-project-2026-07-21/prd.md
source_addendum: ../../prds/prd-bmad-learning-project-2026-07-21/addendum.md
checked_against:
  - DESIGN.md
  - EXPERIENCE.md
date: 2026-07-23
---

# UX Spine Reconciliation

Checked every FR (1–31, 34–35, 40–41 — the PRD's actual numbering skips 32/33/36–39), every NFR (1–7), and every UJ (1–4) against `DESIGN.md` and `EXPERIENCE.md`. Most of the PRD is represented explicitly, often with the FR number cited inline. Two real gaps and one contradiction survived verification; several near-misses turned out to be correctly-scoped omissions.

## Real Gaps

| # | Requirement | What's missing | Suggested fix |
|---|---|---|---|
| 1 | FR1 (registration) / FR18 (admin email edit) | Neither spine names the "email already in use" error state. Every other PRD-mandated error gets a full treatment — exact copy in EXPERIENCE.md's Voice and Tone table and/or a row in the State Patterns table (password mismatch, invalid login, no barbers, double-booking, stale-cancel, admin no-search-results, concurrent-edit conflict) — but the duplicate-email case, required by both FR1 and FR18, has none. A frontend dev has no specified copy or field-retention behavior for this state, unlike the adjacent password-mismatch case which is spelled out in detail (which fields clear, which persist). | Add a "Duplicate email" row to EXPERIENCE.md's State Patterns table (Register + Admin account-edit surfaces) and a copy line to the Voice and Tone table, e.g. "This email is already registered. Use a different email." Mirror the password-mismatch pattern: only the email field should require re-entry, other fields preserved. |
| 2 | NFR1 (login rate-limiting) | The PRD requires login attempts be rate-limited to blunt brute-force guessing (threshold deferred to Architecture, but the behavior itself is a PRD-level requirement, not purely a backend implementation detail). No state, copy, or mention of a rate-limited/lockout condition exists anywhere in either spine — the Login error state only covers "Invalid email or password." | Add a lightweight state to EXPERIENCE.md (even a placeholder, consistent with how the doc already flags other undecided details as "defaults not yet reviewed with the client"), e.g. "Too many attempts. Try again later." — generic enough to not leak account existence, matching the spirit of FR2's anti-enumeration rule. |

## Contradictions

| # | Requirement | Spine statement | Conflict |
|---|---|---|---|
| 1 | FR19 (admin "Create Account" creates barber accounts only) | EXPERIENCE.md's Component Patterns table describes a single "Admin account-edit/create popup" used for both editing and creating, with the same field set including "permission level" and the note "Permission dropdown offers only customer/barber — admin is never an option" — stated without qualification for either mode. | FR19 says the admin Create Account action produces barber accounts only; there's no PRD-described capability for an admin to create a *customer* account (customers only self-register, per FR1/UJ-1). As written, the shared popup implies an admin could pick "customer" at creation time, which the PRD doesn't support. The spine needs to either lock the permission field to "barber" (or omit it) in create mode while keeping the customer/barber dropdown only in edit mode, or explicitly state the field is disabled/pre-set during creation. |

## False Alarms Considered and Dismissed

- **UJ-1's "no forward limit" vs. FR7/spine's 30-day cap.** This is a pre-existing inconsistency inside the PRD itself (the narrative UJ-1 prose says "no forward limit," the authoritative FR7 says "capped at 30 days out"). Both spines correctly follow FR7's more specific, detailed spec (Flow 1: "dates past the 30-day window aren't selectable"). Not a spine defect — the spine resolved an upstream contradiction sensibly rather than introducing one.
- **NFR1's EST/timezone requirement.** Purely a server-side computation detail (what counts as "today," "past," and the 30-minute cutoff) — the frontend just renders whatever availability the backend returns. No UI surface is implied, so its absence from DESIGN/EXPERIENCE is correct, not a gap.
- **NFR4 (automated testing against real SQLite), NFR5 (CI/CD on every push), NFR6 (code organization/maintainability), NFR7 (local-only deployment).** All four are backend/process/engineering requirements with no UI-facing consequence. Correctly absent from both UX documents.
- **Password strength/complexity (Out of Scope).** No minimum is required by the PRD, and neither spine invents one (no complexity hint or validation copy near password fields). Correctly left undesigned.
- **Guest booking, payment processing, notifications, multi-location, audit trail, self-service password reset (all Out of Scope).** None appear anywhere in either spine. Correctly absent.
- **Visual identity (color/type/spacing/shape) going beyond what the PRD specifies.** Per the task instructions, not flagged — the PRD deliberately left visual identity undefined, and DESIGN.md is transparent about which tokens are locked client decisions vs. proposed defaults.
- **FR29 nav label ("Login") vs. EXPERIENCE.md's IA table calling the same button "Sign In."** A cosmetic copy difference, not a behavioral contradiction — the PRD doesn't lock exact button text, and the underlying behavior (destination page, auth-state gating) matches in both documents.
