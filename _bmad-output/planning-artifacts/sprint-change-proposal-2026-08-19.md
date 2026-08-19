---
title: Sprint Change Proposal — z-pax SSO Login
status: approved
created: 2026-08-19
project: bmad-learning-project
---

# Sprint Change Proposal — z-pax SSO Login

## 1. Issue Summary

**Trigger:** New stakeholder requirement (Jack, project owner), raised 2026-08-19 — after all three original epics (17 stories) were fully implemented and retro'd (last retro: Epic 3, 2026-08-18, per `sprint-status.yaml`). This is not a bug or a mid-sprint discovery; it's a net-new feature request landing on top of an already-complete MVP.

**Request:** Add a "Sign in with z-pax" SSO option to the Login page, alongside the existing email/password login. z-pax is an external OAuth2 identity provider (endpoints confirmed: authorize `https://sapi.auth.myzpax.com/connect/authorize`, token `https://sapi.auth.myzpax.com/connect/token`). SSO accounts may be Customer or Barber (admin-promotable) but can never be the system's single Admin account. Client ID/Secret will be provided before implementation and must be stored via environment variables, never committed to source.

**Evidence / requirements gathered during this session:**
- Redirect URI confirmed as `https://localhost:7113/api/auth/sso/callback` (matches the existing `https` launch profile and `api/auth` route prefix)
- OAuth2 authorization-code flow: `client_id`, `scope=offline_access`, `response_type=code`, `redirect_uri` on the authorize request; `client_id`/`client_secret` on the token exchange
- z-pax token lifetimes (session 60 min / access 20 min / refresh 60 min) are **not** adopted by this app — see Decision Log below
- z-pax returns first name, last name, and email after login, used to populate the local `Account` on first SSO sign-in

## 2. Impact Analysis

### Epic Impact
- **Epics 1–3:** No change to scope, ACs, or shipped code paths — untouched. One partial exception: Epic 1/Epic 3 code that assumed `Account.PasswordHash` is always non-null must be reviewed for correctness once it becomes nullable (captured as an explicit AC in new Story 4.1, not a reopening of Epic 1/3's own scope).
- **New Epic 4 — Single Sign-On (z-pax):** added as the next epic, containing 3 new stories (4.1–4.3). See Section 4 for full text.

### Story Impact
- No existing story (1.1–3.5) is modified, reopened, or reverted.
- 3 new stories added: 4.1 (schema/repository), 4.2 (OAuth flow), 4.3 (Login page UI).

### Artifact Conflicts
- **PRD:** New FR42–FR46 (Authentication & Accounts section) and a one-clause addition to NFR1. No existing FR is changed or renumbered.
- **Architecture (`ARCHITECTURE-SPINE.md`):** New AD-19; `Account` entity gains two nullable columns (`SsoProvider`, `SsoSubjectId`) and `PasswordHash` becomes nullable; new partial unique index; Stack table note (no new NuGet package — plain `HttpClient`); Deferred section note on z-pax's own refresh token being deliberately unused.
- **UX:** Login page needs a new "Sign in with z-pax" button/divider and an SSO-specific error-display path (reuses the existing login-error pattern — no new visual state needed beyond what Story 4.3 specifies).
- **Other artifacts (CI/CD, deployment, monitoring):** No impact. No new secrets needed in CI, since automated tests use a fake `ISsoClient` double rather than the live z-pax service (mirrors AD-4's existing DB-isolation principle applied to this external dependency).

### Technical Impact
- **Schema migration:** additive — `PasswordHash` nullable, `SsoProvider`/`SsoSubjectId` added, new partial unique index. No destructive migration.
- **Existing code requiring review, not rewrite:** `AuthService.Login` (must treat `PasswordHash == null` as an automatic failed-login case, not a null-reference risk), registration/admin-edit/admin-create paths in Epic 1/3 that construct or validate `PasswordHash`.
- **New external dependency:** z-pax's OAuth endpoints, called via plain `HttpClient` behind an `ISsoClient` interface (fake double for tests, real implementation for the app) — no new NuGet package.

## 3. Decision Log (key calls made during this session)

| Decision | Choice | Rationale |
|---|---|---|
| Epic structure | New Epic 4, not a reopened Epic 1 | Epic 1 is shipped and retro'd; isolating SSO as new work avoids destabilizing signed-off stories |
| Client ID/Secret storage | Environment variables (`ZPaxSso__ClientId`/`ZPaxSso__ClientSecret`), same as `AdminSeed__*` (AD-6) | Matches existing, already-gitignored `launchSettings.json` convention |
| Authorize/Token endpoint URLs | Regular (non-secret) config, not env vars | Not secret — only credentials need environment-variable protection |
| Account linking | Auto-link by email to an existing password account | Simplest and most user-friendly; linking is additive, doesn't touch the existing password |
| Password survives linking | Yes — both password and SSO remain valid sign-in methods afterward | Linking should never remove an existing capability |
| SSO-only account attempts password login | Same generic "Invalid email or password" as any other failure — no "use SSO instead" message | Preserves FR2's existing no-enumeration guarantee; a distinct message would leak account existence |
| Session mechanism after SSO login | One-time z-pax handshake to fetch identity, then mint our own JWT access token + 15-day refresh cookie (AD-3 unchanged) | Keeps SSO and password sessions identical in every respect (role gating, revocation, session length); avoids building a second, parallel auth-validation path for a low-stakes local demo app |
| SSO role ceiling | Customer or Barber, never Admin | Matches existing single-admin invariant (FR34) exactly |
| Testing against z-pax | Fake `ISsoClient` double in automated tests; real implementation targets live z-pax endpoints | Mirrors AD-4's existing principle of not depending on a live external system in CI |

## 4. Recommended Approach

**Selected: Option 1 — Direct Adjustment** (add Epic 4 within the current project structure).

- Rollback (Option 2): not applicable — nothing is broken or wrong in the shipped epics.
- MVP Review (Option 3): not applicable — the original MVP is already delivered; this is a post-MVP addition, not a scope reduction.

**Effort:** Medium (schema migration + review of existing password-dependent code + new external integration + new UI). **Risk:** Low — purely additive, isolated to the Auth domain, no changes to Booking or Admin-Account domains beyond the nullable-password review. The only real residual risk was credential availability, which is now resolved (Client ID/Secret expected before implementation).

## 5. Detailed Change Proposals

### 5.1 PRD (`_bmad-output/planning-artifacts/prds/prd-bmad-learning-project-2026-07-21/prd.md`)

**Section: Functional Requirements → Authentication & Accounts** — append after FR41:

> - FR42: Any visitor can sign in via z-pax SSO from the Login page, using a dedicated "Sign in with z-pax" option displayed alongside the standard email/password fields.
> - FR43: On first successful z-pax sign-in, if no account exists with a matching email, a new account is created automatically with Role=Customer, using the first name, last name, and email returned by z-pax — no password is set for this account. Attempting to sign in to an SSO-only account via the standard email/password form always fails with the same generic "Invalid email or password" message used for any other failed login (FR2) — never a distinct "use z-pax" message.
> - FR44: On any z-pax sign-in where a local account already exists with a matching email, the user is signed into that existing account (whatever role and password it currently holds) rather than creating a duplicate account — linking does not disable or replace that account's existing password; the user may continue to sign in via either method afterward.
> - FR45: An account created or linked via z-pax SSO is subject to the same single-admin invariant as any other account (FR34) — it can never be or become the system's Admin account; an admin may still promote it to Barber like any other Customer/Barber account (FR18).
> - FR46: Once signed in via z-pax, a user's session, role gating, and access to every existing feature (booking, self-service Account editing, etc.) behave identically to a standard email/password session — SSO is an alternate entry point to the same account model, not a separate one.

**Section: Non-Functional Requirements → NFR1** — append a clause:

> "...z-pax OAuth Client ID/Secret are stored via environment variables only, never committed to source control (same convention as admin-bootstrap credentials, AD-6)."

**Rationale:** Preserves existing FR numbering (no renumbering of FR1–FR41); states behavior/requirements only, leaving mechanism to Architecture; folds the "never Admin" rule into the existing FR34 invariant.

### 5.2 Architecture (`_bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md`)

**New AD-19** (Auth domain, appended after AD-18):

> ### AD-19 — z-pax SSO (OAuth2 Authorization Code)
>
> - **Binds:** FR42–FR46; Auth domain (AuthController/AuthService/AccountRepository)
> - **Prevents:** committing Client ID/Secret to source control; z-pax's token lifecycle leaking into this app's own session model; a duplicate account being created for an email that already exists
> - **Rule:** SSO is folded into the existing Auth trio, not a new domain concept. `GET /api/auth/sso/login` redirects to z-pax's authorize endpoint (`https://sapi.auth.myzpax.com/connect/authorize`) with `client_id`, `scope=offline_access`, `response_type=code`, `redirect_uri=https://localhost:7113/api/auth/sso/callback` (fixed, since NFR7 is local-only). `GET /api/auth/sso/callback` receives `code`, exchanges it at z-pax's token endpoint (`https://sapi.auth.myzpax.com/connect/token`) using `client_id`/`client_secret` from environment variables `ZPaxSso__ClientId`/`ZPaxSso__ClientSecret` (never `appsettings.json`, never committed, same convention as AD-6) — the two endpoint URLs themselves aren't secret and are stored as regular config (`ZPaxSso__AuthorizationEndpoint`/`ZPaxSso__TokenEndpoint` in `appsettings.json`) rather than environment variables. The resulting z-pax access token is used exactly once to fetch identity (email, first name, last name) before being discarded — z-pax's own token/refresh lifecycle is never persisted or relied on afterward. If no `Account` row matches by email, one is created (`Role=Customer`, `PasswordHash=null`, `SsoProvider="zpax"`, `SsoSubjectId=<z-pax subject id>`); if a row already matches by email, that identity is attached to the existing row without touching its `PasswordHash` — both login methods remain valid afterward. Once identity is resolved, the app mints its own access/refresh tokens exactly as `POST /api/auth/login` does (AD-3) — SSO and password sign-in converge on the same session mechanism from that point on. An account with `PasswordHash=null` always fails password-login attempts with the same generic invalid-credentials message as FR2/AD-5 — no distinct "use SSO" message. No account created or linked via SSO can ever be `Role=Admin` (FR34/FR45). Automated tests use a fake `ISsoClient` double rather than the live z-pax service, mirroring AD-4's existing DB-isolation principle.

**Data model update** (Account entity + ER diagram):

```
OLD:
    ACCOUNT {
        int Id PK
        string Email UK
        string PasswordHash
        string FirstName
        string LastName
        string Role
        int SessionVersion
        datetime DeletedAt
        int RowVersion
    }

NEW:
    ACCOUNT {
        int Id PK
        string Email UK
        string PasswordHash "nullable — null for SSO-only accounts"
        string FirstName
        string LastName
        string Role
        int SessionVersion
        datetime DeletedAt
        int RowVersion
        string SsoProvider "nullable"
        string SsoSubjectId "nullable"
    }
```

New partial unique index: `UNIQUE(SsoProvider, SsoSubjectId) WHERE SsoProvider IS NOT NULL` (same pattern as AD-9/AD-15).

**Stack table:** add a note that z-pax integration uses plain `HttpClient` — no OAuth client NuGet package, consistent with the project's existing bias against adding a dependency for a small integration surface.

**Deferred section addition:**

> - z-pax's own refresh token / `offline_access` scope — deliberately unused. We take a one-time identity handshake and mint our own session (AD-19); revisit only if a future need requires re-querying z-pax after initial login.

### 5.3 Epics (`_bmad-output/planning-artifacts/epics.md`)

**Epic List entry:**

> ### Epic 4: Single Sign-On (z-pax)
> A visitor can sign in using their z-pax account as an alternative to email/password — first-time SSO sign-in creates a Customer account automatically from z-pax's identity, a matching email links to an existing account without disturbing its password, and the resulting session behaves identically to a standard login in every other respect.
> **FRs covered:** FR42, FR43, FR44, FR45, FR46

**FR Coverage Map entries:**

```
FR42: Epic 4 - z-pax SSO login option on Login page
FR43: Epic 4 - New account creation from SSO identity
FR44: Epic 4 - Existing-account linking by email
FR45: Epic 4 - SSO accounts subject to single-admin invariant
FR46: Epic 4 - SSO session behaves identically to password session
```

**Story 4.1: Account Schema & SSO-Aware Repository**

As a developer, I want the `Account` entity extended for SSO identities and the repository/service layer updated to handle a nullable password, so that SSO login, linking, and creation can be built as pure business logic on top of a working, tested data layer.

Acceptance Criteria:
- Given no SSO support exists yet, when this story is implemented, then `Account.PasswordHash` becomes nullable, and `SsoProvider`/`SsoSubjectId` (nullable strings) are added via migration, with a partial unique index `UNIQUE(SsoProvider, SsoSubjectId) WHERE SsoProvider IS NOT NULL` (AD-19).
- Given the existing `AuthService.Login` path, when a login attempt targets an account with `PasswordHash = null`, then it fails with the same generic "Invalid email or password" message as any other failed attempt (FR43, FR2) — no distinct SSO-only messaging, and no null-reference error.
- Given the `AccountRepository`, when extended for SSO, then it exposes `FindBySsoIdentity(provider, subjectId)` and `CreateOrLinkSsoAccount(email, firstName, lastName, provider, subjectId)` — the latter creates a new `Role=Customer` account if no email match exists, or attaches the SSO identity to the existing matching account without altering its `PasswordHash` (FR43, FR44).
- Given FR34's admin invariant, when `CreateOrLinkSsoAccount` runs, then it can never create or link to the single admin account, nor ever assign `Role=Admin` (FR45).
- Given every existing code path that assumed `PasswordHash` is always non-null (registration, login, Epic 3's admin account edit/create), when this story is complete, then those paths and their existing tests are reviewed and updated as needed for a nullable `PasswordHash`, with no regression to prior behavior.
- Given the repository, when tested, then every new method — including account-linking and admin-invariant rejection — is covered by xUnit + WebApplicationFactory against a real SQLite instance (NFR4, AD-4).

**Story 4.2: z-pax OAuth Login Flow**

As a visitor or existing user, I want to sign in via z-pax SSO, so that I can access my account without creating a separate password.

Acceptance Criteria:
- Given the Login page, when a user clicks "Sign in with z-pax", then the browser is redirected to z-pax's authorization endpoint (`https://sapi.auth.myzpax.com/connect/authorize`) with `client_id`, `scope=offline_access`, `response_type=code`, and the registered `redirect_uri` (AD-19, FR42).
- Given a successful authorization, when z-pax redirects back to `/api/auth/sso/callback` with a `code`, then the backend exchanges it at `https://sapi.auth.myzpax.com/connect/token` for a z-pax access token, fetches the user's email/first/last name, and resolves the local account via `CreateOrLinkSsoAccount` from Story 4.1 (FR43, FR44).
- Given the resolved account, when the SSO flow completes, then the app mints its own access token (in-memory) and refresh token (HttpOnly cookie) exactly as `POST /api/auth/login` does, and routes the user per FR4 (AD-3, AD-19).
- Given automated tests should never depend on a live external service (mirrors AD-4), when this story is implemented, then the OAuth calls are built against an injected `ISsoClient` abstraction with a fake test double for xUnit coverage, while the real `ISsoClient` implementation targets z-pax's actual endpoints (AD-19) using the real Client ID/Secret expected to be available by implementation time.
- Given z-pax returns an error or the callback is missing/invalid `code`, when this happens, then the user is redirected to Login with an on-screen error, and no account is created or session issued.

**Story 4.3: "Sign in with z-pax" Login Page UI**

As a visitor, I want a clearly visible SSO option on the Login page, so that I can choose either sign-in method.

Acceptance Criteria:
- Given the Login page, when rendered, then it shows the existing email/password fields plus a "Sign in with z-pax" button, visually separated (e.g., a divider), following the existing Button component styling (UX-DR2).
- Given a user clicks "Sign in with z-pax", when the flow starts, then the browser navigates away to z-pax (full redirect, not a popup).
- Given the OAuth flow completes with an error (see Story 4.2), when redirected back to Login, then the error message renders using the existing login-error display pattern.
- Given any viewport width, when the Login page renders with the new SSO option, then the layout remains responsive with no broken/overflowing elements (FR22).

## 6. Implementation Handoff

**Scope classification: Major** (new epic, new architecture decision, new external dependency, data-model change) — but all strategic/architectural decisions were already worked through collaboratively in this session (see Decision Log, Section 3), so no separate PM/Architect replanning pass is needed before implementation.

**Next steps:**
1. Apply the approved edits in Section 5 to the actual source documents (`prd.md`, `ARCHITECTURE-SPINE.md`, `epics.md`).
2. Add Epic 4 and its 3 stories to `sprint-status.yaml` with status `backlog`.
3. Hand off to the Developer agent (Amelia) to create and implement Story 4.1 → 4.2 → 4.3 in order, once Client ID/Secret are available.

**Success criteria:** Epic 4's 3 stories pass their acceptance criteria above, all existing Epic 1–3 tests continue to pass unmodified in outcome (nullable-`PasswordHash` review causes no regression), and CI stays green throughout (NFR5).