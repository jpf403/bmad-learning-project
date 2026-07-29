# Deferred Work

## Deferred from: code review of story-1-3-home-and-about-pages (2026-07-29)

- Home CTA navigates to `/login` (and any unmatched path) with no registered `<Route>`/no catch-all — renders blank until Story 1.5 builds Login; spec explicitly forbids adding a placeholder route now. [frontend/src/App.jsx:12-15]
- `isSignedIn` is never passed by `App.jsx`, so AC#3's signed-in branch is unreachable in the running app, only exercised via direct prop injection in tests — documented temporary auth seam (Stories 1.5/1.6). [frontend/src/pages/Home.jsx:5, frontend/src/App.jsx:13]
- NavBar overflows/doesn't wrap below ~640px, causing horizontal overflow on every page including Home/About — pre-existing (Story 1.1 shell, acknowledged in Dev Notes as Story 1.5's job). [frontend/src/components/NavBar.css]
- `SQLitePCLRaw.lib.e_sqlite3` 2.1.11→2.1.12 bump may not actually contain the CVE-2025-6965 fix (advisory data suggests the fix only ships in the 3.x line) — out of this story's scope (tracked under Story 1.2's changelog), needs external verification. [backend/BarbershopApi/BarbershopApi.csproj:17]
- react-router 8.3.0 requires Node ≥22.22.0 but CI only pins the major version (`node-version: '22'`) — informational risk already flagged in this story's own Dev Notes, not confirmed to break CI. [.github/workflows/ci.yml:31]
