# Sprint Change Proposal — 2026-09-03

**Mode:** Incremental
**Prepared by:** Amelia (Dev)

## 1. Issue Summary

Now that the myzPAX banner (Story 4.4) and its logout wiring (Story 4.5) are shipped, Jack wants to start actually using z-pax's refresh token so the banner's session can outlive z-pax's short access-token lifetime, instead of degrading after 20 minutes as designed.

This directly reopens a decision the architecture had explicitly settled. ARCHITECTURE-SPINE.md's "Deferred" section states: *"z-pax's own refresh token / `offline_access` scope — deliberately unused (AD-19). We take a one-time identity handshake and mint our own session; revisit only if a future need requires re-querying z-pax after initial login."* The FR47 addendum to AD-19 similarly states *"no z-pax-side token-refresh infrastructure is built for this integration; letting the banner degrade after 20 minutes... is an accepted trade-off."* Jack is now asking to build exactly that infrastructure.

Code inspection confirmed this is a real, not cosmetic, change:
- [ZPaxSsoClient.cs:10](../../../backend/BarbershopApi/Services/ZPaxSsoClient.cs) requests scope `"openid profile"` — no `offline_access` — despite AD-19's prose claiming `offline_access` is already requested. The doc and code had already drifted apart.
- `ZPaxTokenResponse` ([ZPaxSsoClient.cs:113](../../../backend/BarbershopApi/Services/ZPaxSsoClient.cs)) has no `refresh_token` field; nothing in the codebase captures or stores a z-pax refresh token today.

Two constraints shape the rollout:
1. z-pax's current configuration for this app is a 20-minute access token / 60-minute refresh token — deliberately mismatched with this app's own 60-minute access token / 15-day refresh token. Jack will have z-pax lengthen both to match this app's lifetimes, but **only after** the refresh mechanism is proven working end-to-end against the current short-lived configuration — not before.
2. A related but separate idea surfaced during discussion: once SSO users have the banner's own logout, should this app's own Account/Logout controls be hidden for SSO-authenticated sessions? Jack wants this deferred, to be revisited during Story 4.6's review period — not built now.

## 2. Impact Analysis

**Epic Impact:** Epic 4 (Single Sign-On) remains fully achievable — no rollback or replan. A new Story 4.6 covers this as additive scope. No epics exist beyond Epic 4, so nothing downstream is affected, and no resequencing is needed.

**Story Impact:** New Story 4.6 (myzPAX Banner Token Refresh) added to Epic 4. No existing story's AC is invalidated — Stories 4.4/4.5 are unaffected by this addition.

**Artifact Conflicts:**
- **PRD:** FR47 needs a second carve-out (alongside FR48's logout carve-out) pointing to a new FR49, which documents the silent-refresh behavior, its degrade-on-failure behavior, and the interim mismatched-token-lifetime state.
- **Architecture:** AD-19 needs a new addendum (same pattern as the existing FR47/FR48 addenda) covering the scope change, the new `zpaxRefreshToken` cookie, the new `ISsoClient.RefreshAccessToken` method, and the new `GET /api/auth/sso/zpax-refresh` endpoint. The Deferred section's claim that `offline_access`/refresh is "deliberately unused" is now false and is removed. A new Deferred item is added for the hide-Account/Logout-for-SSO-users idea, explicitly earmarked for Story 4.6's review period.
- **UX:** No conflict — the banner's visible behavior is unchanged, only its effective lifetime extends.
- **Testing:** `FakeSsoClient` needs the new refresh method; new xUnit coverage for the refresh endpoint's cookie-present/absent/z-pax-rejects paths; new Vitest coverage (with fake timers) for the frontend's proactive-refresh scheduling and degrade-on-failure behavior (AD-4 — real coverage, no new mocking framework).

**Technical Impact / Flagged Risk:** Whether z-pax's token endpoint actually honors `grant_type=refresh_token` the way expected is unverified going in — same "flag it, build it, live-verify before done" discipline already used for Story 4.4's `currentAppId` and Story 4.5's logout redirect target. The failure mode is low-severity and self-contained: if refresh doesn't work, the banner simply degrades to its existing fallback strip, exactly as it does today — this app's own session is never at risk either way, so there's no forced-logout consequence to a failed refresh while token lifetimes remain mismatched.

## 3. Recommended Approach

**Direct Adjustment (Option 1)** — no rollback, no MVP/scope change.

- New Story 4.6 within Epic 4. Effort: Medium (new OAuth scope, new cookie, new backend endpoint, new frontend refresh-scheduling logic, plus tests). Risk: Medium, concentrated entirely in the flagged z-pax refresh-endpoint-behavior assumption — everything else follows established patterns already proven in Stories 4.4/4.5.

Rejected alternatives: Rollback (Option 2) isn't relevant — nothing needs reverting, this is additive work on top of a working baseline. MVP Review (Option 3) isn't warranted — this doesn't change MVP scope or core goals.

## 4. Detailed Change Proposals

### 4.1 PRD (`prd.md`)

**FR47 — OLD:**
> ...This is a UI/navigation feature only, with one exception: the banner's logout control ends this app's session too — see FR48. Everything else about authentication, session, and role-gating behavior is unaffected (FR46 still holds).

**FR47 — NEW:**
> ...This is a UI/navigation feature only, with two exceptions: the banner's logout control ends this app's session too (FR48), and the banner's displayed session is kept alive past z-pax's short access-token lifetime via silent token refresh (FR49). Everything else about authentication, session, and role-gating behavior is unaffected (FR46 still holds).

**New FR49:**
> A user signed in via z-pax SSO has their myzPAX banner session kept alive beyond z-pax's access-token lifetime (currently 20 minutes) without re-authenticating: the backend silently exchanges a stored z-pax refresh token for a new z-pax access token on the visitor's behalf, and the frontend adopts it transparently. If the refresh itself fails (e.g. z-pax's refresh token, currently valid 60 minutes, has expired), the banner degrades to its own built-in fallback strip exactly as it does today when no token is available — this app's own session is unaffected either way. This is an interim state: by the end of this story's implementation, z-pax's access- and refresh-token lifetimes for this app are changed to match this app's own (60-minute access token, 15-day refresh token) — not during the story itself, so the refresh mechanism can first be proven against z-pax's current short-lived configuration. Once aligned, the degrade case becomes rare in practice, not eliminated architecturally.

### 4.2 Epics (`epics.md`)

**FR summary catalog (near top of file) — ADD:**
```
FR48: Epic 4 - myzPAX banner logout ends the app session too
FR49: Epic 4 - myzPAX banner session kept alive via silent z-pax token refresh
```
(FR48 was missing from this catalog since the prior sprint-change-proposal — closed here for consistency.)

**Epic 4 summary — OLD tail:**
> ...An SSO-authenticated session also sees the myzPAX cross-app navigation banner on every page, and signs out through the banner's own logout control rather than the app's Logout menu item.
> **FRs covered:** FR42, FR43, FR44, FR45, FR46, FR47, FR48

**Epic 4 summary — NEW tail:**
> ...An SSO-authenticated session also sees the myzPAX cross-app navigation banner on every page, kept alive past z-pax's short access-token lifetime via silent refresh, and signs out through the banner's own logout control rather than the app's Logout menu item.
> **FRs covered:** FR42, FR43, FR44, FR45, FR46, FR47, FR48, FR49

**New Story 4.6:**
```
### Story 4.6: myzPAX Banner Token Refresh

As an SSO-authenticated user,
I want the myzPAX banner to stay alive for as long as I'm signed in,
So that I don't lose access to the cross-app launcher just because z-pax's
access token is short-lived.

**Acceptance Criteria:**

**Given** the "Sign in with z-pax" flow (Story 4.2)
**When** the backend builds the z-pax authorization URL
**Then** the requested scope is exactly `"openid profile offline_access"`
(previously `"openid profile"`), so z-pax's token response includes a
refresh token (FR49, AD-19)

**Given** a successful token exchange at z-pax's token endpoint
**When** the response includes a `refresh_token`
**Then** it is captured (`ZPaxTokenResponse`) and, at `SsoCallback`, stored
in a new `zpaxRefreshToken` cookie — HttpOnly+Secure+SameSite=Strict,
scoped to `/api/auth/sso` — set alongside the existing `zpaxAccessToken`
and `zpaxIdToken` cookies (FR49, AD-19)

**Given** a signed-in session whose z-pax access token is nearing or past
its lifetime
**When** the frontend proactively calls a new `GET /api/auth/sso/zpax-refresh`
endpoint (`[Authorize]`'d via this app's own session)
**Then** the backend reads the `zpaxRefreshToken` cookie, calls z-pax's
token endpoint with `grant_type=refresh_token` and the stored refresh
token, and on success returns the new z-pax access token in the response
body — overwriting the `zpaxRefreshToken` cookie if z-pax returns a
rotated refresh token, so the next refresh doesn't use a stale one (FR49,
AD-19)

**Given** the frontend holds a z-pax access token in memory (Story 4.4)
**When** the session is active
**Then** it schedules a call to `GET /api/auth/sso/zpax-refresh` ahead of
the current z-pax access-token lifetime (20 minutes today) and adopts the
returned token transparently, with no visible interruption to the banner
(FR49)

**Given** the refresh call fails — no cookie present, or z-pax rejects the
refresh token (e.g. its 60-minute lifetime, in effect during this story,
has elapsed)
**When** this happens
**Then** the banner degrades to its own built-in fallback strip exactly as
it does today when no token is available — no error surfaced, and this
app's own session is completely unaffected either way (FR49)

**Given** automated tests should never depend on a live external service
(mirrors AD-4)
**When** this story is implemented
**Then** `FakeSsoClient` gains the new refresh method, the endpoint's
cookie-present / cookie-absent / z-pax-rejects paths are covered by xUnit
+ `WebApplicationFactory`, and the frontend's refresh-scheduling and
degrade-on-failure logic are covered by Vitest with fake timers (NFR4,
AD-4)

**Given** the refresh mechanism has been proven to work end-to-end with a
live z-pax SSO session
**When** this story is marked done
**Then** z-pax's configuration for this app has been changed to a
60-minute access-token lifetime and a 15-day refresh-token lifetime,
matching this app's own token lifetimes — this alignment happens at the
end of the story, not before, so the refresh mechanism is first proven
against z-pax's original short-lived configuration (FR49)
```

### 4.3 Architecture (`ARCHITECTURE-SPINE.md`)

**New addendum, appended after the existing "myzPAX banner logout (FR48)" block:**
```
**myzPAX banner token refresh (FR49):** the authorization request built by
`BuildAuthorizationUrl` (AD-19) now requests scope `"openid profile
offline_access"` (previously `"openid profile"`), so z-pax's token
response includes a `refresh_token` alongside `access_token`/`id_token`.
`ZPaxTokenResponse` captures it, and `SsoCallback` stores it in a new
`zpaxRefreshToken` cookie — HttpOnly+Secure+SameSite=Strict, path
`/api/auth/sso` — set alongside the existing `zpaxAccessToken` (2-minute,
single-use) and `zpaxIdToken` (15-day) cookies. A new `ISsoClient` method,
`RefreshAccessToken(string refreshToken)`, POSTs to z-pax's token endpoint
with `grant_type=refresh_token` and the stored token; a new endpoint,
`GET /api/auth/sso/zpax-refresh` (`[Authorize]`'d via this app's own
session), reads the cookie, calls it, and returns the new z-pax access
token in the response body — overwriting `zpaxRefreshToken` if z-pax
returns a rotated refresh token, so a stale one is never reused. The
frontend schedules a call to this endpoint ahead of the z-pax access
token's lifetime and adopts the result transparently; if the refresh
fails for any reason (missing cookie, z-pax rejects the token), the
banner degrades to its own built-in fallback exactly as it does today
when no token is available (Story 4.4) — this app's own session is
unaffected either way, matching FR49's degrade-silently behavior. This is
built and live-verified against z-pax's original short-lived configuration
(20-minute access / 60-minute refresh) first; only once proven does z-pax
change this app's configured lifetimes to 60-minute access / 15-day
refresh, matching this app's own tokens (Story 4.6).
```

**Deferred section — REMOVE this now-inaccurate line:**
```
- z-pax's own refresh token / `offline_access` scope — deliberately unused
  (AD-19). We take a one-time identity handshake and mint our own session;
  revisit only if a future need requires re-querying z-pax after initial
  login.
```

**Deferred section — ADD:**
```
- **Hiding in-app Account/Logout UI for SSO-authenticated users** —
  deferred; currently the in-app Account and Logout controls remain
  visible and available to every account, SSO or password, as a fallback
  (FR48). Revisit during Story 4.6's review period whether SSO-authenticated
  visitors should have these hidden now that the myzPAX banner provides
  its own logout control.
```

### 4.4 Sprint status (`sprint-status.yaml`)

```yaml
development_status:
  epic-4: in-progress
  ...
  4-5-myzpax-banner-logout: done
  4-6-myzpax-banner-token-refresh: backlog
```

## 5. Implementation Handoff

**Scope classification: Minor** — implementable directly by the Developer agent (Amelia) with no PO/PM/Architect replan needed.

- **Developer agent (Amelia):**
  - Implement Story 4.6 (create-story → dev-story flow), including the manual live-SSO-session verification gate before marking done, and the z-pax config-alignment step (60-min access / 15-day refresh) as the final step of the story, not before.
- **Success criteria:**
  - `offline_access` added to the scope; z-pax refresh token captured and stored in `zpaxRefreshToken`.
  - `GET /api/auth/sso/zpax-refresh` implemented and covered by xUnit; frontend refresh-scheduling covered by Vitest.
  - Live manual test confirms the banner survives past z-pax's original 20-minute access-token lifetime without re-authentication.
  - Live manual test confirms a failed refresh (simulated or by waiting past the 60-minute refresh-token lifetime) degrades the banner silently with no impact to the app's own session.
  - z-pax's app configuration changed to 60-min access / 15-day refresh only after the above is verified.
