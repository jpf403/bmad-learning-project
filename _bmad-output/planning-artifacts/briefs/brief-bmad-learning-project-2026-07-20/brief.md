---
title: Barbershop Appointment Scheduler — BMad Method & DORA Demo
status: final
created: 2026-07-20
updated: 2026-07-21
---

# Product Brief: Barbershop Appointment Scheduler (BMad/DORA Demo Project)

## Executive Summary

This is a small web application for booking appointments at a fictional barbershop — customers pick a barber, a date, and an available time slot; barbers and an admin manage what happens behind that booking. Built with a .NET backend, a React frontend, and a SQLite database, with three distinct roles (customer, barber, admin) enforced through hashed-password authentication.

The app itself is not the point. It exists to give its builder a real, small-scale project to run through the BMad Method end to end, and to demonstrate — to a boss evaluating that capability — that both the method and DORA principles (deployment frequency, automated validation) can be applied in practice, not just described. The scope is deliberately kept small enough to finish solo in roughly a week and a half, so the process stays the thing under the microscope.

"Done" means two things exist side by side: a working app that demonstrates auth, role separation, and real booking logic against a live database, and a separate written reflection on how the BMad Method's AI-driven passes actually unfolded — what changed, when, and why — across the run.

## The Problem

**Inside the story:** customers want a haircut without waiting hours in a walk-in line or needing someone to actually pick up the phone to book a time. The shop has no way to show real-time availability, so booking today means guessing, calling repeatedly, or just showing up and hoping there's an open chair.

**Outside the story:** understanding of a structured method like BMad, or of DORA principles, is easy to claim and hard to verify secondhand. A slide deck or a verbal walkthrough asks a boss to take the builder's word for it; a line about "AI-assisted development" doesn't show whether the AI's changes were reviewed, tested, or even understood by the person directing it.

Without a concrete artifact, the only evidence available is self-report. The cost of that status quo: the boss either has to trust the claim without inspection, or construct a real evaluation exercise from scratch — a poor use of anyone's time for what should be a quick capability check.

This project closes that gap by producing something inspectable: a live app, a visible CI/CD pipeline, and a written trail of the AI's actual decisions during the build — evidence, not testimony.

## The Solution

Any logged-in user — customer, barber, or admin alike — can visit the booking page, pick a barber and a date from a calendar (past dates disabled, no artificial booking-window limit), and see a dropdown of only the times that are actually still open that day. Booking an already-taken slot is blocked with a clear error — a backstop behind a UI that shouldn't let it happen in the first place.

Role separation shows up elsewhere: a separate dashboard page gives barbers a view of their own upcoming appointments, and gives admins a view of every appointment across every barber, plus full account management — promoting a customer to barber or admin, and creating barber or admin accounts directly. The booking page is shared; the dashboard is where the roles actually diverge.

The build itself is the demo: automated tests cover the booking-conflict logic, auth, and role permissions; a CI pipeline runs them on every push. The single planned "wow" moment is live — pushing a small, low-risk change during the walkthrough and watching the pipeline go green in real time, so deployment frequency and automated validation are witnessed, not described.

## Success Criteria

This project succeeds if, by the end, the following are true:

- **Authentication works end-to-end** — hashed-password login gates access; unauthenticated users can't reach role-specific pages.
- **Three roles are structurally distinct** — customer, barber, and admin each have a different dashboard experience, while all three can still use the shared booking page.
- **At least one real database interaction is demonstrably live** — booking a slot writes to SQLite, and the write is reflected back (e.g., the slot disappears from availability).
- **The booking-conflict logic holds** — double-booking a slot fails with a clear error, and already-taken slots don't appear as options in the first place.
- **DORA principles are visible, not just described** — a CI pipeline runs automated tests on every push, keeping the codebase in an always-deployable state (continuous delivery, even with no live deploy target), and at least one live push during the walkthrough is watched going green in real time.
- **The codebase is maintainable, not just functional** — clear organization such that a reader (including future-you) can locate where a given piece of behavior lives without hunting. DORA's lead-time-for-changes principle is about the codebase supporting fast, confident changes, not the pipeline alone.
- **A written process reflection exists** — a document, separate from the app, that traces what the AI changed, when, and why across the BMad Method's passes, in enough detail that a reader can follow the reasoning, not just the outcome.

Success does not require the app to look polished, scale, or support anything beyond local runs — those aren't the bar being cleared here.

## Scope

**In scope for this build:**
- Homepage (hero, tagline, CTA, at least one hover interaction), About page, and a shared booking page usable by any logged-in role
- Hashed-password authentication with three roles: customer, barber, admin
- A role-specific dashboard: barbers see their own appointments; admins see all appointments plus account management (promote to barber/admin, create barber/admin accounts)
- SQLite-backed booking with conflict validation (reject double-booked slots; filter already-taken slots out of what's shown), within fixed business hours in 30-minute increments (9:00 AM–5:00 PM, last bookable slot 4:30 PM)
- An automated test suite covering DB CRUD, auth, and role permissions, run in CI on every push
- A live-demo moment: a small push during the walkthrough, watched going green in CI in real time
- The written process-reflection document, as its own deliverable alongside the app

**Explicitly out of scope:**
- Payment processing
- Email/SMS reminders or notifications of any kind
- Password reset flow
- Multi-location support
- Mobile responsiveness — **superseded during PRD (2026-07-22):** reversed after further discovery; the PRD now requires responsive layout across mobile and desktop. See `prds/prd-bmad-learning-project-2026-07-21/prd.md` FR22/NFR3, which is current source of truth on this point. Left as originally written here for historical record.
- An actual deploy target or public hosting — the app runs locally only. "Continuous delivery" here means the pipeline keeps the codebase always deployable, not that it is deployed anywhere.

## Who This Serves

**Inside the story — who the app is built for:**
- **Customers (primary).** Want to book a haircut without calling and waiting on hold — real, current availability, and a time slot that's actually free.
- **Barbers (secondary).** Want to see their own day of scheduled appointments without digging through everyone else's.
- **Shop admin (tertiary).** Wants oversight across all barbers and easy control over who has staff access, without touching the database directly.

**Outside the story — who this project actually serves:**
- **The boss, as reviewer (primary).** Needs confidence that the BMad Method and DORA principles were genuinely applied — evidence, not testimony. Success for them is "I can see it, I don't have to trust it."
- **The builder, as presenter (secondary).** Needs a demo that's walkable live without fragility, and a written reflection that captures the method's passes as they happened, not reconstructed afterward.

The product decisions get made as if the customers, barbers, and admin were real — that's what makes the demo credible — but the actual purpose and audience of the whole exercise are the second list.

## Deliverables & Timeline

Two deliverables:
1. The working app — the barbershop scheduler as scoped above, runnable locally, with CI wired up on GitHub.
2. A written process reflection — a separate document tracing what the AI changed, when, and why across the BMad Method's passes, drafted from the method's own decision trail (memlogs, this brief's history) rather than reconstructed from memory afterward.

**Constraints:**
- Solo build, no other contributors.
- No hard deadline from the boss. Soft personal target: finished by Friday. Realistic outer bound if it runs long: roughly a week and a half of workdays.
