# Sprint Change Proposal — 2026-08-31

**Mode:** Incremental
**Prepared by:** Amelia (Dev)

## 1. Issue Summary

Two issues surfaced after Story 4.4 (myzPAX Cross-App Navigation Banner) shipped:

**Issue A — SSO logout doesn't end the barbershop session.** The myzPAX banner's logout control is a widget-vendor feature: without an `onLogout` callback wired up, clicking it only ends the visitor's z-pax SSO session — it clears the widget's own caches and navigates to its configured `logoutUrl`, but has no way to reach into this app's `AuthContext` or revoke its server-side refresh session. Confirmed via [MyzpaxBanner.jsx:35-39](../../../frontend/src/components/MyzpaxBanner.jsx) that `MyzpaxBanner.init` is currently called with `getToken`/`currentAppId`/`position` only. This isn't a missed AC — Story 4.4 never specified `onLogout` — it's a new requirement: Jack wants SSO-authenticated visitors to log out via the banner (ending both sessions and landing back at z-pax login), while the existing in-app Logout stays available to every account as a backup for now.

**Issue B — Mobile nav dropdown shows no items.** Below the 1023px breakpoint, the collapsed hamburger menu opens but shows no navigable options, leaving visitors on small screens with no way to change pages. Investigation found the underlying React/Radix logic in [NavBar.jsx:67-97](../../../frontend/src/components/NavBar.jsx) is correct — it maps `visibleLinks` into the dropdown, and all 16 `NavBar.test.jsx` tests pass, including two that specifically assert the collapsed menu renders menu items. `git log` confirms this markup predates Epic 4 (Story 4.4 only touched 5 lines of NavBar.jsx, for the full-page-logout-navigation fix) — so this is not a regression from the SSO/banner work. It's also not new: [sprint-status.yaml:96](../../../_bmad-output/implementation-artifacts/sprint-status.yaml) shows this exact bug class was a retro action item from Epic 1 ("NavBar overflow bug below ~640px"), marked resolved in Story 2.2 by adding this very markup — it has resurfaced as a real-browser rendering issue that unit tests (jsdom has no layout engine) can't catch.

## 2. Impact Analysis

**Epic Impact:** Epic 4 (Single Sign-On) remains fully achievable — no rollback or replan. A new Story 4.5 covers Issue A as additive scope. Issue B carries no epic/story impact at all; it's tracked as a defect action item, matching how this exact bug class was already handled once before.

**Story Impact:** New Story 4.5 (myzPAX Banner Logout) added to Epic 4. No existing story's AC is invalidated — Story 4.4's AC is unaffected by this addition. No future epics exist beyond Epic 4, so nothing downstream is affected.

**Artifact Conflicts:**
- **PRD:** FR47 explicitly claimed the banner "does not affect authentication, session, or role-gating behavior (FR46 still holds)" — the new logout wiring makes that false as written, so FR47 needs a carve-out and a new FR48 is added. FR46 gets a one-line pointer to the exception.
- **Architecture:** `ZPaxSsoOptions`/AD-19 needs an addendum documenting the `onLogout` teardown-then-redirect flow and its known unverified assumption (see below). No new AD number — appended to AD-19 following the same pattern as the existing "myzPAX banner support (FR47)" addendum.
- **UX:** No conflict for either issue — the banner's logout control is vendor UI, and the nav bug is an implementation defect against an already-approved design, not a design change.
- **Testing:** `MyzpaxBanner.test.jsx` needs new coverage for the `onLogout` wiring (AD-4 — real coverage, no new mocking).

**Technical Impact / Flagged Risk:** The `onLogout` redirect target is planned to reuse the same URL that starts the "Sign in with z-pax" flow (`GET /api/auth/sso/login`), on the assumption that navigating a visitor back through it also ends their z-pax session. This is unverified — if their z-pax cookie is still live, it could silently re-authenticate them instead of logging them out. The failure mode is loud and immediately observable (the visitor lands back in the app still signed in, right after clicking "log out"), so the agreed approach is try-then-fix: implement with this URL, verify manually with a live SSO session before marking Story 4.5 done, and if it fails, find z-pax's actual end-session endpoint and swap the config value — no architecture rework needed either way. This mirrors Story 4.4's own precedent of flagging `currentAppId` as unverified going into that story.

## 3. Recommended Approach

**Direct Adjustment (Option 1)** for both issues — no rollback, no MVP/scope change.

- Issue A: New Story 4.5 within Epic 4. Effort: Medium (frontend `onLogout` wiring + config value + PRD amendment + manual live-session verification). Risk: Medium, entirely concentrated in the flagged redirect-target assumption above — low risk everywhere else since it reuses the existing logout-teardown code path.
- Issue B: Standalone defect-fix task, no new story. Effort: Low (root-cause a real-browser CSS/stacking issue; existing unit tests already cover the logic side). Risk: Low.

Rejected alternatives: Rollback (Option 2) isn't relevant — nothing needs reverting, both issues are additive/defect work on top of a working baseline. MVP Review (Option 3) isn't warranted — neither issue changes MVP scope or core goals.

## 4. Detailed Change Proposals

### 4.1 PRD (`prd.md`)

**FR47 — OLD:**
> ...This is a UI/navigation feature only: it does not affect authentication, session, or role-gating behavior (FR46 still holds).

**FR47 — NEW:**
> ...This is a UI/navigation feature only, with one exception: the banner's logout control ends this app's session too — see FR48. Everything else about authentication, session, and role-gating behavior is unaffected (FR46 still holds).

**FR46 — OLD:**
> Once signed in via z-pax, a user's session, role gating, and access to every existing feature (booking, self-service Account editing, etc.) behave identically to a standard email/password session — SSO is an alternate entry point to the same account model, not a separate one.

**FR46 — NEW:**
> ...SSO is an alternate entry point to the same account model, not a separate one. The one difference is how the session ends: see FR48.

**New FR48:**
> A user signed in via z-pax SSO ends their session using the myzPAX banner's logout control rather than this app's own Logout menu item. Triggering it tears down this app's session — clearing the in-memory access token and revoking the server-side refresh session, identical to what the app's own Logout does — then navigates the browser to z-pax to end the SSO session. The app's own Logout control remains available to every account, SSO or password, as a fallback for now; this may change in the future.

### 4.2 Epics (`epics.md`) — new Story 4.5

```
### Story 4.5: myzPAX Banner Logout

As an SSO-authenticated user,
I want the myzPAX banner's logout control to end both my z-pax session and my barbershop session together,
So that logging out from the banner actually signs me out of this app too, not just the SSO layer.

**Acceptance Criteria:**

**Given** a signed-in session holding a z-pax access token in memory (Story 4.4)
**When** `MyzpaxBanner.init` is called
**Then** it is passed an `onLogout` callback in addition to `getToken`/`currentAppId`/`position` (FR48)

**Given** the visitor triggers the banner's logout control
**When** `onLogout` fires
**Then** it tears down this app's session exactly as the existing NavBar Logout does — calling the app's logout endpoint to revoke the server-side refresh session, then clearing `AuthContext` — before navigating away (FR48, mirrors existing `NavBar.handleLogout`)

**Given** the session has been torn down
**When** `onLogout` completes its app-side teardown
**Then** the browser is redirected via `window.location.assign` to the same URL the "Sign in with z-pax" button uses (`GET /api/auth/sso/login`) (FR48)

**Given** that redirect target is unverified against z-pax's actual session-termination behavior
**When** this story is implemented
**Then** it's manually verified end-to-end with a live z-pax SSO session before the story is marked done — if the visitor lands back in the app still signed in, the fallback is to find z-pax's real end-session endpoint and swap the config value; flagged going in as unverified, same as Story 4.4's `currentAppId` (FR48)

**Given** the existing in-app "Logout" menu item (`NavBar.jsx`)
**When** this story is implemented
**Then** it is left completely unchanged and remains available to every account — SSO or password — as a fallback (FR48, no regression)

**Given** the new `onLogout` wiring
**When** tested
**Then** it's covered in `MyzpaxBanner.test.jsx` — asserting the callback clears the session and navigates to the SSO login URL — without introducing any new mocking beyond what the existing suite already stubs (AD-4)
```

### 4.3 Architecture (`ARCHITECTURE-SPINE.md`) — addendum to AD-19

```
**myzPAX banner logout (FR48):** `MyzpaxBanner.init` (FR47) is now also passed an `onLogout` callback. Without it, the banner's logout control only ends the z-pax session — this app's own session (in-memory access token, server-side refresh session) would survive untouched. `onLogout` performs the same teardown as the existing NavBar Logout — revoke the server-side refresh session, clear `AuthContext` — then does a full-page navigation (`window.location.assign`, not client-side routing, for the same reason AD-19's existing Logout flow uses one) to the same URL `GET /api/auth/sso/login` that starts the SSO sign-in flow, on the assumption that navigating a visitor back through it also ends their z-pax session. This is unverified against z-pax's actual behavior and is called out as such in Story 4.5 — manual verification with a live SSO session gates marking the story done, mirroring Story 4.4's own precedent for the unverified `currentAppId`. The existing in-app Logout control (AD-3) is unchanged and remains available to every account, SSO or password, as a fallback.
```

### 4.4 Sprint status (`sprint-status.yaml`) — new action item

```yaml
  - epic: 4
    action: "Mobile nav dropdown menu (collapsed hamburger, <1023px) renders no visible menu items despite correct logic and 16/16 passing unit tests - root-cause as a real-browser CSS/stacking issue (jsdom can't catch it) and fix. This is the same bug class the Epic 1 retro flagged and Story 2.2 supposedly resolved - it has resurfaced, so verify the fix holds this time before closing."
    owner: "Amelia (Dev)"
    status: open
```

## 5. Implementation Handoff

**Scope classification: Minor** — both items are implementable directly by the Developer agent (Amelia) with no PO/PM/Architect replan needed.

- **Developer agent (Amelia):**
  - Implement Story 4.5 (create-story → dev-story flow), including the manual live-SSO-session verification gate before marking done.
  - Root-cause and fix the mobile nav dropdown defect per the new sprint-status action item.
- **Success criteria:**
  - Story 4.5: `onLogout` wired, `MyzpaxBanner.test.jsx` covers it, and a live manual test confirms the visitor is actually signed out of both the app and z-pax (not silently re-authenticated).
  - Nav defect: mobile dropdown visibly shows and navigates to all `visibleLinks` in a real browser below 1023px, confirmed by manual check in addition to the existing passing unit tests.
