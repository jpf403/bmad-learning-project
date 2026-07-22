---
title: Barbershop Appointment Scheduler — Addendum
source_brief: brief.md
---

# Addendum

Detail carried over from the brainstorming phase and this brief's conversation that is too granular for the brief itself but should inform the PRD/architecture stage.

## Business Rules & Design Decisions

### Business Rules

- Appointment slots run 9:00 AM–5:00 PM in fixed 30-minute increments: 9:00 AM, 9:30 AM, 10:00 AM, 10:30 AM, 11:00 AM, 11:30 AM, 12:00 PM, 12:30 PM, 1:00 PM, 1:30 PM, 2:00 PM, 2:30 PM, 3:00 PM, 3:30 PM, 4:00 PM, 4:30 PM. 4:30 PM is the last bookable slot since the business day ends at 5:00 PM.

### Options Considered — Booking Window Cap

- Considered capping bookable dates to a 7-day window, paired with the calendar UI. Dropped as an arbitrary simplification with no real business justification. Final decision (at brief stage): calendar allows any future date; only past dates are disabled/darkened.
- **Superseded during PRD (2026-07-21/22):** revisited after PRD review found that an unbounded forward window, combined with no shop-closed-day model, let customers book real slots on days the shop can't honor. The PRD now caps forward booking to 30 days (~1 month) — see `prds/prd-bmad-learning-project-2026-07-21/prd.md` FR7. Unlike the 7-day option rejected above, this cap is grounded in that concrete risk rather than being an arbitrary simplification. The PRD is the current source of truth on this point; this brief is left as-is for historical record.

### Options Considered — Date/Time Selection UI

- Considered three approaches to date/time selection before landing on the current design:
  - Option A — free-text date/time entry: easiest to build, but impractical/ugly (user gambles on availability). Rejected.
  - Option B — day-of-week dropdown (auto-resolving to date) feeding an available-times dropdown: moderate effort, no guessing. Favored as the practical middle ground.
  - Option C — full interactive calendar widget: most polished, but needs a calendar library and more build effort.
  - Final: hybrid of B+C — calendar widget for date selection, dropdown showing only the available times for the selected day.

## Testing Notes

### Testing Tooling (informational starting points, not locked decisions)

- .NET side: xUnit + WebApplicationFactory
- Frontend: Vitest/Jest + React Testing Library
- Optional end-to-end: Playwright

### Specific Test Cases Called Out

- Booking an already-taken slot is rejected with a clear error (double-booking prevention).
- Already-booked slots are filtered out of the available-times picker shown to the user.
