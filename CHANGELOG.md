# Changelog

All notable changes to **this template** are documented here (not changes to projects started *from* it). Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Caching:
  - HybridCache (in-memory L1 + Redis L2, with stampede protection) for user reads, including the `/users/me` lookup the SPA makes on every page load.
  - Output caching for the user list (shared between authorized callers, gated by `users:read`) and the backend for frontend's `/providers`.
  - User writes invalidate both layers by tag. Redis is added to the AppHost. See ADR 011.

- Permission-based authorization. `users:read`, `users:write` and `users:delete` are Keycloak client roles, bundled into an `admin` realm role and enforced by per-permission policies. Users can always update their own profile; updating anyone else's requires `users:write`. `GET /users/me` returns the caller's permissions, and a users admin page is shown only to users who hold them. The dev realm includes an `admin` account. See ADR 010.

- Third-party sign-in (Google, GitHub, Microsoft) brokered by Keycloak. Each provider is enabled only when its credentials are configured in the AppHost, and the sign-in page shows "Continue with …" buttons for enabled providers. See ADR 009.
- `AppTemplate.BackendForFrontend.Tests`, the first automated tests for the backend for frontend.

- Password reset: a "Forgot your password?" page and an anonymous, throttled `POST /password-reset` that has Keycloak email a reset link through its Admin API, without revealing which emails have accounts. Mailpit catches the emails in development. See ADR 008.

- Authentication: Keycloak (OpenID Connect) for register/login/logout, a new `AppTemplate.BackendForFrontend` host that keeps the session in a secure HTTP-only cookie and proxies `/api` with the user's access token, and JWT Bearer validation on the Web API. Domain users are provisioned just in time via `GET /users/me`. See ADR 007.
- Conventional Commits and Gitflow conventions for this repository (`CLAUDE.md`, `.github/CONTRIBUTING.md`).

- Clean Architecture .NET 10 backend (`Core` / `UseCases` / `Infrastructure` / `Web`), based on [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture), with a `User` feature as an end-to-end reference vertical slice.
- Angular frontend (standalone components, `core`/`shared`/`features` structure), talking to the API via a dev-time proxy instead of CORS.
- .NET Aspire `AppHost` orchestrating Postgres, the Web API, and the Angular frontend for local development.
- `scripts/rename-template.sh` to rename the `AppTemplate` placeholder to a real project name.
- xUnit test projects: `AppTemplate.UnitTests` (Core/UseCases, NSubstitute) and `AppTemplate.FunctionalTests` (full HTTP pipeline via `WebApplicationFactory`, EF Core InMemory).
- Hugo documentation site (`docs/`) with getting-started, design-decisions, and architecture decision records, deployed to GitHub Pages. Light/dark theme toggle, defaulting to dark.
- GitHub Actions CI: cross-platform backend build/format/test, frontend build/format/lint/test, monthly CodeQL scan, docs deploy.
- Dependabot for NuGet, npm, and GitHub Actions dependencies.
- ESLint (`@angular-eslint`) and Prettier for the frontend; csharpier for the backend, enforced in CI and applied automatically to staged files by a git pre-commit hook (Husky.Net, auto-installed on first build after cloning).
- `USAGE.md`, `CONTRIBUTING.md` (in `.github/`), and this changelog.

### Fixed

- The users list query's raw SQL didn't select every mapped column (`email`, `external_id`), which breaks entity materialization on Postgres.

- Two nullable-reference warnings (CS8602) when mapping a user without a phone number in the list and get-by-id endpoints.

- The backend for frontend and the Web API couldn't start the OpenID Connect flow outside Development, because the Aspire service-discovery authority isn't HTTPS. `Keycloak:Authority` now overrides it.

- A stale Angular test asserting markup that no longer existed.
- Dead `[Required]` Data Annotations on FastEndpoints request DTOs (FastEndpoints validates via FluentValidation, not Data Annotations - these had no effect).
- Missing `.AsNoTracking()` on read-only EF Core specifications.
- A cross-platform line-ending mismatch (`.editorconfig` required CRLF while committed blobs were LF) that passed CI on Windows but failed csharpier's format check on Linux/macOS.

### Changed

- `POST /users` was removed (users now register in Keycloak), every `/users` endpoint requires authentication, and `User` has an `ExternalId` (Keycloak `sub`) in place of a password.
- The Angular dev server's `proxy.conf.ts` was removed; the backend for frontend is now the single browser origin.

- Template license set to MIT (the source project it was based on used AGPL-3.0, which is unsuitable for a reusable starting point).

[Unreleased]: https://github.com/andregabrielmelo/dotnet-angular-aspire-template/commits/main
