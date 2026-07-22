---
title: Barbershop Appointment Scheduler — PRD Addendum
source_prd: prd.md
---

# Addendum

Technical leanings and details surfaced during PRD discovery that inform the Architecture phase but don't belong in the PRD's capability-level narrative.

## Auth Session Mechanism (leaning, not locked)

- Password storage: industry-standard salted hashing (e.g., ASP.NET Core Identity's built-in PBKDF2 `PasswordHasher`, or bcrypt/Argon2id) — never a fast general-purpose hash (MD5/SHA-256 alone) and never plaintext.
- Session maintenance: user leans toward **token-based** session handling (e.g., JWT bearer tokens) rather than server-side cookie sessions, given the React frontend + .NET API split. Final call (JWT vs. secure HTTP-only cookie, token expiry/refresh strategy) deferred to Architecture (Winston).

## First-Admin Bootstrap (leaning, not locked — supports FR31)

- **Never commit the actual seed email/password to source control.** `appsettings.json` in the repo should only ever contain empty/placeholder keys (e.g., `AdminSeed:Email` / `AdminSeed:Password` = `""`); real values are supplied locally via `dotnet user-secrets` and, for CI, via a GitHub Actions secret / environment variable — never a committed file.
- Suggested pattern for .NET + SQLite: on application startup (e.g., in `Program.cs` after `Database.Migrate()`, or in a small `IHostedService`), check whether any account with the admin role exists. If none does, create exactly one admin account using credentials supplied via configuration — hashed through the same path as any normal account.
- This avoids a UI-exposed backdoor and matches a standard ASP.NET Core Identity seeding pattern.
- Final call on exact seeding mechanism deferred to Architecture.
- Per FR34, this seeded account is the only admin account that will ever exist — no application code path promotes another account to admin or demotes/deletes this one. Architecture doesn't need to design for multiple admins or an admin-management flow beyond this single seed.

## Session Invalidation on Admin-Driven Password Change (leaning, not locked — supports FR35)

- Given the JWT leaning above, forcibly ending all of an account's active sessions the moment an admin changes that account's password (FR35) needs a revocation mechanism, since JWTs are normally stateless.
- Suggested pattern: store a per-account "session version" counter; stamp each issued token with the version at issuance; on every protected request, compare the token's version to the account's current version and reject on mismatch.
- Incrementing the counter on an admin-driven password change instantly invalidates every previously issued token for that account without needing a full token blacklist.
- Final call deferred to Architecture.

### Design Implication for Permission-Level Changes (FR35)

- Since a page refresh — not a full re-login — is meant to pick up a new permission level, the token's role claim can't be trusted as long-lived truth the way a full session-version check works for passwords.
- Two ways to satisfy this: (a) don't bake role into the token at all — check the account's current role against the database on every request; or (b) issue a short-lived token that's silently re-fetched/refreshed often enough that a page refresh reliably picks up the new role.
- Either way, role must be treated as closer to "live" than password-session-version is.
- Final call deferred to Architecture.

## Testing Tooling (carried forward from brief addendum — informational starting points, not locked decisions)

- .NET side: xUnit + WebApplicationFactory
- Frontend: Vitest/Jest + React Testing Library
- Optional end-to-end: Playwright
