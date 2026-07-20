---
project: Barbershop appointment scheduling web app
date: 2026-07-20
source: brainstorm-barbershop-appointment-app-2026-07-20/.memlog.md
---

# Project Intent

A portfolio/demo web app for a fake barbershop's appointment scheduler, built to demonstrate the BMad Method and DORA principles to reviewers. Scoped deliberately small — buildable in a short timeframe, not weeks — favoring a few polished, well-tested features over broad scope.

# Stack & Structure

- **Backend:** .NET
- **Frontend:** JS/React
- **Database:** SQLite
- **Auth:** hashed-password login, three roles — customer, barber, admin
- **Pages:** homepage, about, scheduling (plus a separate staff dashboard — see Role Structure)

# Decided Design Directions

## Scheduling UX
- Calendar date-picker for date selection. Past dates are disabled/darkened; there is **no artificial day-window limit** (an earlier idea to cap booking to a 7-day window was considered and explicitly dropped as an arbitrary simplification with no real business justification).
- Once a date is picked, a dropdown shows **only the available times** for that day (availability is resolved at the time-dropdown level, not shown on the calendar itself).

## Booking-Conflict Validation
- Booking an already-taken slot must fail cleanly with a clear error message (double-booking prevention).
- Already-taken slots are filtered out of what's shown to the user in the time picker, so the conflict case is a backstop, not the primary UX.
- This validation logic is shared/core: it underlies the scheduling UI, both staff dashboards, and the test suite alike — treat it as one system, not three separate features.

## Role Structure
- A separate staff dashboard, hidden from customers, is the mechanism for making roles feel distinct — no additional UI badge/label is needed on top of it.
- **Barber view:** sees their own scheduled appointments; can cancel them.
- **Admin view:** sees all appointments across all barbers, plus an account-management page to promote a customer to barber or create barber accounts directly.

## Testing Strategy
- Highest-value automated test targets: database CRUD, login/auth, and role-based permissions.
- Specific test cases called out:
  - Booking an already-taken slot is rejected with a clear error (double-booking prevention).
  - Already-taken slots are filtered out of the available-times picker.
- Suggested tooling (offered by the facilitator as informational starting points, not locked decisions): xUnit + WebApplicationFactory (.NET side); Vitest/Jest + React Testing Library; optionally Playwright for end-to-end flows.

## DORA / CI-CD Demo Strategy
- The single highest-value "wow" moment identified: make the CI/CD pipeline's *effects* visible live, not just documented.
- Plan: leave a GitHub Actions tab open during the walkthrough, push a small/low-risk change (e.g. a text update) live during the presentation, and watch automated tests run to green in real time.
- This simultaneously demonstrates deployment frequency and automated validation (DORA + autotesting in one moment).

## Homepage (secondary priority)
- Hero section: stylized scissors graphic, brief shop tagline/info, and a prominent "Schedule an Appointment" CTA.
- At least one hover micro-interaction — e.g. the scissors graphic animating behind the CTA button on hover.
- Explicitly flagged by the user as lower priority / secondary polish relative to the other themes.

# Priority Ranking (user-stated)

1. Validation/booking-conflict logic and role structure (staff dashboards, admin account management) — highest priority.
2. DORA/CI-CD pipeline visibility — equally important.
3. Homepage polish — secondary.
