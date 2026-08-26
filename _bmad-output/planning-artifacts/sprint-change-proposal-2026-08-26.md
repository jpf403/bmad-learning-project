---
title: Sprint Change Proposal — myzPAX Cross-App Navigation Banner
status: approved
created: 2026-08-26
updated: 2026-08-26
project: bmad-learning-project
---

# Sprint Change Proposal — myzPAX Cross-App Navigation Banner

## 1. Issue Summary

**Trigger:** New stakeholder requirement (Jack, project owner), raised 2026-08-26 — after Epic 4's original three stories (4.1–4.3, z-pax SSO login) were fully implemented and merged. Not a bug or a mid-sprint discovery; a net-new feature request landing on top of an already-shipped SSO integration.

**Request:** After signing in with z-pax SSO, show a header/banner on every page that pulls in the other myzPAX-suite apps the signed-in user is entitled to. z-pax provides a hosted script for this (`banner.js`) — a single `MyzpaxBanner.init({ getToken, currentAppId, ... })` call, where `getToken` supplies the app's z-pax SSO access token. The widget calls z-pax's own `GET /api/v1/my-apps` on its own; the app never sees the entitlement list. If the token is missing or invalid, the widget fails safe to a minimal "Return to myzPAX" strip rather than a broken panel.

**Evidence / decisions gathered during this session:**
- Confirmed against the actual codebase (not just the architecture doc) that the z-pax access token obtained today in `ZPaxSsoClient.ExchangeCodeForIdentity` is used once to fetch identity, then fully discarded — nothing persists it or threads it to the frontend (`ARCHITECTURE-SPINE.md` AD-19 said the same, so doc and code agreed here).
- An initial draft of this proposal assumed a persisted z-pax **refresh** token was needed. Jack corrected this: the vendor's own integration guide only asks for an access token and explicitly designs for graceful degradation when it goes stale — no refresh infrastructure is actually required. This significantly simplified the change (no new DB column).
- Separately caught a documentation/code mismatch while investigating: the architecture doc's AD-19 states the authorize request uses `scope=offline_access`; the real `ZPaxSsoClient.cs` uses `scope="profile"` only, with a `[DEBUG-TEMP]` comment noting scope was dropped from the token request "while debugging with z-pax." Not touched by this change (no refresh token is being requested), but worth Jack's attention separately since it means the architecture doc has been inaccurate on this point since Story 4.2 shipped.
- z-pax access tokens have a fixed 20-minute lifetime (confirmed by Jack) — acceptable for this project's demo/testing purpose; no token-refresh mechanism is being built to extend it.
- Banner only works for SSO-authenticated sessions — a password-only login never obtains a z-pax token, so entitlement data doesn't exist for it. Confirmed with Jack: SSO-only is the intended scope, not a gap to fill.
- `currentAppId` value to use: `barbershop_demo`, per Jack, but explicitly unconfirmed against z-pax's actual launcher registry — carried into Story 4.4 as an AC to verify before merge.

## 2. Impact Analysis

### Epic Impact
- **Epics 1–3:** No change — untouched.
- **Epic 4 (Single Sign-On):** Extended, not reopened. Stories 4.1–4.3 remain as shipped; no rollback. One new story appended: 4.4 (myzPAX Cross-App Navigation Banner). Epic 4's summary sentence updated to mention the banner.

### Story Impact
- No existing story (1.1–4.3) is modified, reopened, or reverted.
- 1 new story added: 4.4.

### Artifact Conflicts
- **PRD:** New FR47 (Authentication & Accounts section), additive — no existing FR changed or renumbered.
- **Architecture (`ARCHITECTURE-SPINE.md`):** AD-19 amended with a new paragraph covering the banner's token hand-off (short-lived single-use cookie + new `GET /api/auth/sso/zpax-token` endpoint); AD-19's "Binds" line extended to FR47. **No schema migration** — no new DB column, unlike the discarded refresh-token approach. Deferred section left untouched (still accurate — no `offline_access` scope is being requested).
- **UX:** New UX-DR21 — banner placement (below Nav bar, `position: 'static'`, vendor default style/layout) for SSO sessions only. No new component build (the widget is a self-contained closed-Shadow-DOM script); no new State Pattern needed (the widget owns its own degraded-state UI).
- **Other artifacts (CI/CD, deployment, monitoring):** No impact. No new secrets — the endpoint reuses the existing SSO cookie/session machinery; tests stub the banner script and the cookie paths, no live z-pax dependency introduced (mirrors AD-4).

### Technical Impact
- **Schema migration:** none.
- **Backend:** `ZPaxSsoClient.ExchangeCodeForIdentity` needs to also surface the raw z-pax access token (currently only identity fields are returned); `AuthController.SsoCallback` sets a new short-lived cookie; one new endpoint (`GET /api/auth/sso/zpax-token`).
- **Frontend:** `AuthContext` bootstrap gains one additional fetch call; a new small component mounts the banner script conditionally, below `NavBar`.
- **External dependency risk:** `currentAppId` is unverified; the vendor's launcher-registry entry for this app needs confirming before Story 4.4 is considered done.

## 3. Decision Log (key calls made during this session)

| Decision | Choice | Rationale |
|---|---|---|
| Banner scope | SSO-linked accounts only | Password-only accounts have no z-pax token; there's nothing to show them. Confirmed with Jack — not treated as a gap. |
| Token persistence | None — no refresh token requested or stored | Corrected mid-session: the vendor's own docs design for graceful degradation on a stale/missing token; building refresh infrastructure would reintroduce exactly the complexity AD-19's Deferred section had already declined ("z-pax's own refresh token / offline_access scope — deliberately unused"). |
| Token hand-off mechanism | Short-lived (2-min), single-use HttpOnly cookie + dedicated pickup endpoint | Reuses the existing `ssoState` short-lived-cookie pattern already in `AuthController`; avoids ever putting a bearer token in a URL or persisting it server-side. |
| z-pax token storage on frontend | In-memory only, alongside the app's own access token | Mirrors AD-3's existing in-memory-only philosophy; naturally lost on hard refresh — an accepted trade-off given the 20-minute token lifetime anyway. |
| `currentAppId` value | `barbershop_demo` (unconfirmed) | Best information available from Jack; flagged as a Story 4.4 AC to verify before merge rather than blocking this proposal on it. |
| Banner placement | Below Nav bar, `position: 'static'`, vendor default style/layout | Widget is a closed-Shadow-DOM script — can't be restyled by this app's design tokens, so placement is the only real UX decision; `static` matches the app's existing non-sticky layout convention. |

## 4. Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Extend Epic 4 with one new story (4.4); amend the PRD (new FR), Architecture (AD-19 addendum, no schema change), and UX design requirements (new UX-DR) in place. No rollback of Stories 4.1–4.3 needed — they remain correct and complete as shipped. No PRD MVP scope reduction — this is additive scope on top of an already-complete MVP.

- **Effort:** Medium (new backend endpoint + cookie plumbing + frontend conditional-mount wiring + tests). No DB migration.
- **Risk:** Low — isolated to the existing Auth domain; no changes to Epics 1–3 or to Stories 4.1–4.3's existing behavior; degrades gracefully by design if the external dependency (z-pax's banner API) is ever unavailable.
- **Timeline impact:** One additional story in Epic 4; no resequencing of other epics.

## 5. Detailed Change Proposals

### PRD (`prd.md`)
Added FR47 under Authentication & Accounts (see diff applied 2026-08-26).

### Architecture (`ARCHITECTURE-SPINE.md`)
- AD-19 "Binds" line extended to FR42–FR47.
- New paragraph appended to AD-19's Rule describing the `zpaxAccessToken` cookie hand-off and the new `GET /api/auth/sso/zpax-token` endpoint (see diff applied 2026-08-26).
- Deferred section: no change (verified still accurate).

### Epics (`epics.md`)
- New UX-DR21 (banner placement/config).
- FR Coverage Map: added `FR47: Epic 4 - myzPAX cross-app navigation banner`.
- Epic 4 summary sentence (both occurrences) extended to mention the banner; FRs-covered list extended to include FR47.
- New Story 4.4: "myzPAX Cross-App Navigation Banner" with 7 acceptance criteria (see full text in `epics.md`), covering: the cookie hand-off on SSO callback, the pickup endpoint's three outcomes (present/absent/consumed), frontend bootstrap wiring, conditional mounting (present vs. absent token), the unverified `currentAppId` value as an explicit AC, and test coverage expectations (backend cookie-path coverage via `WebApplicationFactory`, frontend conditional-mount coverage via Vitest with the banner script stubbed).

### Sprint tracking (`sprint-status.yaml`)
- Added `4-4-myzpax-cross-app-navigation-banner: backlog` under `epic-4`.

## 6. Implementation Handoff

**Scope classification: Moderate.** Backlog reorganization (one story added to an already-in-progress epic) plus documentation amendments across PRD/Architecture/UX — no fundamental PM/Architect replan required, but the architecture amendment is significant enough (reversing language in a previously-closed decision) that it's captured here rather than left implicit.

- **Developer agent (Amelia):** Implement Story 4.4 per the acceptance criteria above, following the dev-story workflow (TDD per task). Before marking the story done, confirm the `currentAppId` value against z-pax's actual launcher registry (AC6) — do not assume `barbershop_demo` is correct without checking.
- **Jack (Project Lead):** Separately worth a look — the architecture doc vs. code mismatch on SSO scope (`offline_access` documented, `profile` actually used, with a `[DEBUG-TEMP]` comment on the abandoned scope in the token request) was noticed during this session but is out of scope for Story 4.4 since no refresh token is being requested either way. Flagging it so it doesn't get lost.

**Success criteria:** Story 4.4 implemented and tested per its ACs; an SSO-authenticated user sees the myzPAX banner on every page; a password-only user never sees it or any related network/script activity; `currentAppId` verified against z-pax's registry before the story is marked done.
