# Version Verification Review — ARCHITECTURE-SPINE.md

**Reviewed:** `_bmad-output/planning-artifacts/architecture/architecture-bmad-learning-project-2026-07-23/ARCHITECTURE-SPINE.md`
**Method:** Live web search against each named technology/version in the Stack table and version-specific claims elsewhere in the spine. "As of" date used: 2026-07-24.

## Overall Verdict

The Stack table holds up well overall — nearly every entry matches what's actually current or was current, and the two narrative claims (`PasswordHasher<T>` and `Microsoft.AspNetCore.RateLimiting` "bundled with .NET 10") are both correct and non-obvious enough that they read as genuinely researched rather than assumed. One entry is stale: **Vite 8.0.16** is a real, shipped version, but it is not current — Vite has since moved to 8.1.x (8.1.5 confirmed as latest at review time, published ~7 days prior). Everything else checked out as accurate or reasonably current.

## Item-by-Item Findings

### .NET 10 — "LTS, supported to 2028-11" — VERIFIED
.NET 10 released November 11, 2025, and is the LTS release with 3 years of support. Sources differ slightly on the exact end day (Nov 10 vs Nov 14, 2028), but the spine only claims month-level precision ("2028-11"), which is correct either way.

### ASP.NET Core Web API 10 — `dotnet new webapi --use-controllers` — VERIFIED
Confirmed real and necessary: since .NET 8, `dotnet new webapi` defaults to a minimal-API template, so `--use-controllers` is required to scaffold a controller-based project. Microsoft Learn's "Create a controller-based web API" tutorial is versioned for `aspnetcore-10.0` and reflects this flag. Fits the stated purpose exactly.

### EF Core 10.0.10 / Microsoft.EntityFrameworkCore.Sqlite 10.0.10 — VERIFIED
Both `Microsoft.EntityFrameworkCore` 10.0.10 and `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 are published, real NuGet packages, versions match each other as expected for an EF Core release train.

### Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9 — VERIFIED (current at time of writing)
Confirmed on NuGet; 10.0.9 was the latest published patch as of early-to-mid June 2026 and no later 10.0.x version turned up in search as of the review date. Minor curiosity: this trails EF Core's 10.0.10 by one patch number, but that's expected — different repos, independent release cadences, not an inconsistency.

### ASP.NET Core Identity `PasswordHasher<T>` — "bundled with .NET 10" — VERIFIED
`PasswordHasher<T>` lives in `Microsoft.Extensions.Identity.Core`, which carries `<IsAspNetCoreApp>true</IsAspNetCoreApp>` and ships inside the `Microsoft.AspNetCore.App` shared framework — no separate `PackageReference` is needed for a project using the ASP.NET Core web SDK. Claim is accurate and non-trivial (easy to get wrong, since Identity is often assumed to always need a NuGet package).

### Microsoft.AspNetCore.RateLimiting — "bundled with .NET 10 (no third-party package)" — VERIFIED
Confirmed part of `Microsoft.AspNetCore.App.Ref` for .NET 7, 8, 9, and 10 — a `using Microsoft.AspNetCore.RateLimiting;` directive is sufficient, no `PackageReference` required. Matches the stated purpose (login-endpoint sliding-window rate limiting) exactly.

### React 19.2.8 — VERIFIED, current
Confirmed as the latest published version on npm, last published ~2 days before the review date. Precise and accurate — this looks genuinely checked, not guessed.

### Vite 8.0.16 — **STALE, flag**
8.0.16 is a real, published patch version, but it is **not current**. At review time the latest Vite release was **8.1.5**, published about a week prior. The spine states a specific patch version with no qualifier, which reads as more current/authoritative than it is. Not a functional problem (8.0.16 still exists and works), but worth knowing it's already a minor version behind. Separately: Vite 8 switched to a Rolldown-powered build pipeline by default — a substantive underlying change from Vite 5/6/7 that the spine doesn't call out; if that matters for reproducibility/tooling assumptions elsewhere in the project it's worth a sentence, though it doesn't invalidate the "official React JS (non-TS) template" claim — that template variant still exists in `create-vite`.

### xUnit.v3 3.2.2 — VERIFIED, current, and correctly recommended over v2
xUnit.net v3 3.2.2 released 2026-01-14. xUnit v2 is officially in maintenance mode (security fixes only); all new feature work is v3-only. The spine's implicit judgment call (use v3, not v2) matches the project's own stated guidance.

### Vitest 4.1.10 — VERIFIED, current
Confirmed as latest on npm, published ~July 6, 2026 (about 2-3 weeks before review date).

### @testing-library/react 16.3.2 — VERIFIED, current (per available data)
Confirmed as the latest published version on npm; no later 16.x version surfaced in search. Note it's a relatively quiet package (last published ~6 months prior to review date per one source), so "current" here means "nothing newer exists," not "recently updated" — worth being aware of but not a red flag.

### @testing-library/jest-dom "6.x" — appropriately non-specific
Stated as a floating major version rather than a pinned patch, unlike every other row. This is actually the right level of confidence for something not independently verified to the patch — no issue, but noting the asymmetry: every other entry is pinned to a specific version; this one alone is deliberately vague, suggesting it either wasn't checked as closely or was intentionally left flexible.

### @testing-library/user-event 14.6.1 — VERIFIED, current
Confirmed as the latest published version; package is mature/stable (last published about a year before review date), consistent with it having genuinely not changed rather than being outdated commentary.

### Playwright 1.61.1 — VERIFIED, current stable
Confirmed 1.61.1 (June 23, 2026) is the latest **stable** release as of the review date. A 1.62.0 alpha existed (dated 2026-07-21) but was not yet a stable release, so 1.61.1 is the correct "optional e2e" pin.

### AD-12 — "EST" = America/New_York, "correctly DST-aware" — VERIFIED
Confirmed: since .NET 5/6, `TimeZoneInfo.FindSystemTimeZoneById` resolves IANA IDs like `America/New_York` cross-platform (via ICU), including correct DST transitions, on both Windows and Linux. The claim is accurate and the specific mechanism (as opposed to a naive hardcoded UTC-5 offset, which the spine explicitly calls out as the failure mode being avoided in AD-12's "Prevents" line) is exactly right.

## Summary Table

| Entry | Status |
| --- | --- |
| .NET 10 LTS to 2028-11 | Verified |
| ASP.NET Core `--use-controllers` | Verified |
| EF Core 10.0.10 | Verified |
| EFCore.Sqlite 10.0.10 | Verified |
| JwtBearer 10.0.9 | Verified |
| PasswordHasher<T> bundled | Verified |
| RateLimiting bundled | Verified |
| React 19.2.8 | Verified |
| Vite 8.0.16 | **Stale — current is 8.1.5** |
| xUnit.v3 3.2.2 | Verified |
| Vitest 4.1.10 | Verified |
| @testing-library/react 16.3.2 | Verified |
| @testing-library/jest-dom 6.x | Not independently pinned (acceptable) |
| @testing-library/user-event 14.6.1 | Verified |
| Playwright 1.61.1 | Verified |
| AD-12 DST-aware America/New_York | Verified |
