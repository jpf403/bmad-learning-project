# Review — ARCHITECTURE-SPINE.md (Barbershop Appointment Scheduler)

Reviewer: independent, adversarial pass against the 6-point checklist. Not part of the original coaching conversation; verdicts below are argued from the spine text plus the `.memlog.md` decision trail and live version checks, not from assumed good faith.

## Overall

The spine is unusually disciplined for a document this size: every AD has Binds/Prevents/Rule, the Rules are concrete enough to code against directly, and the memlog shows real trade-off reasoning (not just assertion) behind the harder calls (SessionVersion vs role-in-token, non-rotating refresh token, computed-not-stored status). That said, it has two omissions material enough to blow past the "no whole dimension left silent" bar, plus one stale/vague stack pin and one punted UX decision that will genuinely cause two independently-built components to diverge. None of these are nitpicks about phrasing — they're places where a second builder, reading only this spine, would have to invent an answer that a sibling builder could invent differently.

## Finding 1 (High) — Frontend routing is completely undecided

The Structural Seed puts a `pages/` directory under `frontend/src/`, and AD-2/AD-3 hinge the entire security model on per-request role and session checks — but nothing in the Stack table, the ADs, or the Structural Seed says how the client gets from URL to page, or how a customer/barber/admin-only page is gated on the frontend. No React Router (or hand-rolled switch) is named. This is exactly the kind of structural dimension the checklist flags: not "deferred" with a rationale, just silent. Two builders could reasonably land on React Router with route-level guards, a single top-level switch on `auth.role`, or ad-hoc per-page redirects in `useEffect` — all of which produce different file layouts, different failure modes for the "demoted user has a stale tab open" scenario the memlog explicitly reasoned about for the backend (line 28), and different amounts of duplicated auth-check logic. Given the backend half of this exact problem got a dedicated AD (AD-2), the frontend half deserved at least a one-line Rule ("React Router v6 data routers; role gating via a `<ProtectedRoute>` wrapper reading `GET /api/auth/me`" or similar).

## Finding 2 (High) — Radix UI is dropped from the spine despite being a locked UX constraint

`.memlog.md` line 20 records: "frontend is custom CSS/components except Radix UI primitives for calendar, dropdown/select, and modal" — a constraint sourced from DESIGN.md and treated as settled during the coaching conversation. It does not appear anywhere in ARCHITECTURE-SPINE.md: not in the Stack table, not in the Structural Seed's `components/` note. A build-substrate document whose whole job is "keep independently-built units consistent" cannot drop a named dependency during distillation — anyone building the calendar or modal from the spine alone (which is the document's stated purpose — it's meant to precede epics/stories, not require re-reading DESIGN.md) would have no signal to reach for Radix rather than hand-rolling it or picking a different headless-UI library. This is a distillation bug, not a coaching-conversation gap — the decision was made, it just didn't survive into the spine.

## Finding 3 (Medium) — The two UX open items are punted without an owner or resolution path

Deferred correctly flags that no error/warning color exists yet and the tablet breakpoint is named but not sized in pixels, and correctly notes "both need a decision before the components that depend on them are built." But it stops there. This is precisely the case checklist item 3 warns about: form-validation error states and responsive breakpoints are exactly the kind of thing two components built at different times will independently guess at (one dev picks a red, another an orange; one hardcodes 768px, another 900px), and the spine — the document whose job is to prevent that — explicitly declines to either make the call or name who/what makes it before the dependent components are built. "Not an architecture-level call" may be true, but then the spine should say where the call does get made (a design-tokens file? the first story that touches it, locked and then binding for all others?), not leave it open-ended.

## Finding 4 (Low) — `@testing-library/jest-dom` pinned as "6.x" while every sibling package gets an exact version

Live check: current npm latest for `@testing-library/jest-dom` is **7.0.0** (a major bump that makes `@testing-library/dom` a required peer dependency — not a no-op upgrade). The Stack table pins `@testing-library/react` to 16.3.2, `user-event` to 14.6.1, `xUnit.v3` to 3.2.2, etc., all exact — jest-dom alone gets the vague "6.x" the checklist explicitly calls out as a red flag, and it's also now a major version behind current. Trivial to fix (pin an exact 6.x patch, or take 7.0.0 and add the peer dep), but as written it's the one pin in the table that isn't actually a pin.

## Minor/version notes (not independently blocking)

- Vite is pinned at 8.0.16; current latest at review time is 8.1.5 — one minor version behind. Not wrong, just worth a re-check before scaffolding since patch pins age fast on a template this new.
- .NET 10 / EF Core 10.0.10 / JwtBearer 10.0.9 are plausible for a Nov-2025 GA product patched monthly through mid-2026, and React 19.2.8 and Playwright 1.61.1 both check out as exactly current — no other version in the table looks stale or implausible.
- AD-1's layering rule has no automated enforcement (NFR6 in the memlog is explicitly "assessed by manual review, not a test") — acceptable for a solo project but worth flagging since every other AD with a testable claim (AD-9's index, AD-5's rate limit, AD-4's test stack) is directly verifiable in CI while this one relies on the same person remembering their own rule months later.
- No AD id collisions, no leftover template placeholders (`TBD`/`Lorem ipsum`/etc.), and all 13 ADs carry populated Binds/Prevents/Rule fields — checklist item 6 passes clean.
- The memlog's own stated scope ("purpose: build-substrate + fuller human-facing doc") implies a second companion document; only the spine exists in this folder. Not a spine-content defect, but worth confirming whether that second doc was intentionally dropped or is still outstanding.

## Checklist verdicts

1. Divergence points fixed / misses none — **Fails**: routing (Finding 1) and the UI-primitives dependency (Finding 2) are real divergence points left unfixed.
2. Every Rule enforceable — **Passes**, with the AD-1 manual-review caveat noted above (not disqualifying).
3. Nothing in Deferred allows harmful divergence — **Fails**: the UX open items (Finding 3) are a live divergence risk, not a safely-deferred one.
4. Stack is verified-current, nothing vague/stale — **Fails on one line**: jest-dom's "6.x" (Finding 4); everything else checks out.
5. Every structural dimension decided/deferred/flagged — **Fails**: frontend routing is silent, not deferred (Finding 1).
6. No placeholders/duplicate ids/missing fields — **Passes** clean.
