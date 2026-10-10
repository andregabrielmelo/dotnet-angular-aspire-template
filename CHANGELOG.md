# Changelog

All notable changes to **this template** are documented here (not changes to projects started *from* it). Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Removed

- `EnqueueMissedWelcomeEmailsJob` (the hourly `enqueue-missed-welcome-emails` recurring job), `WelcomeEmailJob` and `IBackgroundJobScheduler`. The welcome email now goes through the transactional outbox, so its enqueue can no longer be lost, and the safety net it needed is gone. On an existing deployment, the old recurring job stays in Hangfire storage and is skipped as unknown until `POST /v1/admin/jobs/restore` (or a restart) removes it.

### Added

- Transactional outbox and inbox (ADR 016): entities raise integration events that are saved with them and delivered at least once by a Hangfire relay. Claims use `FOR UPDATE SKIP LOCKED` with leases, failures back off and are dead-lettered, a per-handler inbox makes redelivery safe, and `POST /v1/admin/outbox/dead-letters/requeue` retries dead letters. The welcome email is its first consumer. A domain event handler that fails after the save no longer turns the request into a 500. See `docs/content/reliability-semantics.md`.
- Business metrics (`ApplicationMetrics`): users provisioned, welcome emails sent, job runs and durations, outbox messages. Plus Npgsql and EF Core metrics and Npgsql spans.
- End-to-end tests (`AppTemplate.EndToEndTests`, `e2e.yml`): Aspire starts the whole stack and Playwright drives registration, sign-in, profile editing and sign-out. A `/profile` page lets users rename themselves. They found that "Create account" opened Keycloak's sign-in form (Keycloak ignores `prompt=create` sent through PAR), now fixed.
- Health probes in every environment: `/alive`, `/health` (readiness: Postgres only) and `/health/dependencies` (Redis, reports Degraded). With Redis down, cache calls skip it at once instead of waiting out its timeout.
- One redacting logging pipeline (Serilog to the console and OTLP) for the API and the backend for frontend, with one line per request. `[PersonalData]`/`[SecretData]`, sensitive property names and email/JWT/bearer patterns are redacted. `LoggingBehavior` now actually runs for commands and queries.
- A migration drift check (`MigrationDriftTests`). It found, and a migration fixed, a leftover `DEFAULT ''` on `users.external_id`.
- A global rate limiter (by validated `sub` or client address, 429 problem details with `Retry-After`), trusted forwarded headers from configured proxies only, and request timeouts (504). FastEndpoints `Throttle` is replaced by named rate-limit policies, because it keyed on the spoofable `X-Forwarded-For` header.
- Security headers: a strict CSP and anti-framing headers on the SPA, `nosniff` everywhere, and `no-store` on authenticated API responses. `npm run csp:check` fails the build on inline scripts.
- "When to abstract" and "supported vs required" guidance in the design decisions.

- Typed API contract: `scripts/export-openapi.sh` writes the committed `backend/openapi/v1.json`, and `npm run api:generate` turns it into TypeScript types that the Angular feature models alias. CI fails when either is stale.
- Secret scanning: `secret-scan.yml` runs gitleaks over the full git history on every PR and on pushes to `main`/`develop`.
- `AppTemplate.ArchitectureTests`: enforces the layer dependency rules (project references and compiled type dependencies) and the endpoint and handler placement conventions. See ADR 006.
- `AGENTS.md` and `.github/instructions/`: agent-neutral coding rules, which `CLAUDE.md` now defers to.

- Job management, structured after netrock's Jobs feature:
  - Recurring jobs are `IRecurringJobDefinition`s, scheduled at startup and run through a DI-activated `RecurringJobRunner`.
  - Admins can list, trigger, pause/resume (persisted, and survives restarts), remove and restore jobs through `/admin/jobs` (`jobs:read`, `jobs:manage`) and the new Angular `/jobs` pages.
  - Startup and Restore keep the scheduler in sync with the code: recurring jobs whose definition was renamed or deleted are removed.
  - Concurrent pause, resume or remove requests on the same job all succeed instead of returning 500.
  - See ADR 013.

- Background jobs with Hangfire, stored in the application's Postgres. A welcome email goes out after sign-up: idempotent, retried, and sent through Mailpit in development. An hourly job syncs user names and emails from Keycloak. The jobs dashboard is available in Development. See ADR 012.

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

- **Security:** FastEndpoints' `GET /_test_url_cache_` route, which lists every route and endpoint type name, was reachable anonymously in every environment. An authorization fallback policy now requires authentication for any route that declares no authorization. See ADR 010.
- `AppTemplate.BackendForFrontend.Tests` was missing from the solution, so CI never ran it.
- User lookups no longer cache "not found", so requests for ids that don't exist can't fill the cache.
- A Redis outage turned successful user writes into 500s, because cache invalidation threw after the database write. Invalidation is now best-effort and logs the failure.
- `MimeKitEmailSender` disconnected with an already-cancelled token, so every send threw after delivering the message. It now takes a `CancellationToken` and no longer logs recipient addresses.
- The development `Mailserver` setting used the key `Server` instead of `Hostname`, so it was ignored.

- The users list query's raw SQL didn't select every mapped column (`email`, `external_id`), which breaks entity materialization on Postgres.

- Two nullable-reference warnings (CS8602) when mapping a user without a phone number in the list and get-by-id endpoints.

- The backend for frontend and the Web API couldn't start the OpenID Connect flow outside Development, because the Aspire service-discovery authority isn't HTTPS. `Keycloak:Authority` now overrides it.

- A stale Angular test asserting markup that no longer existed.
- Dead `[Required]` Data Annotations on FastEndpoints request DTOs (FastEndpoints validates via FluentValidation, not Data Annotations - these had no effect).
- Missing `.AsNoTracking()` on read-only EF Core specifications.
- A cross-platform line-ending mismatch (`.editorconfig` required CRLF while committed blobs were LF) that passed CI on Windows but failed csharpier's format check on Linux/macOS.

### Changed

- **Breaking:** the API is versioned. Every route moved under `/v1` (`GET /v1/users`, `POST /v1/password-reset`, ...), and the browser calls `/api/v1/...` through the backend for frontend. Endpoints declare `Version(ApiVersions.V1)`, and a test fails for one that doesn't. See ADR 015.
- Frontend:
  - `npm run format` and `npm run format:check` replace the hand-typed prettier commands, and CI uses them.
  - `strict` and `strictTemplates` are now explicit; TypeScript 6 and Angular 22 already applied them by default.
  - The users page calls a new `UsersService`. ESLint now rejects `inject(HttpClient)` outside service files.

- **Breaking:** every error response is now an RFC 9457 problem details document with a `traceId`, in both the API and the backend for frontend (ADR 014).
  - Validation errors use ASP.NET Core's `errors` shape instead of FastEndpoints' `ErrorResponse`.
  - 401, 403 and 404 responses have a body.
  - `Conflict`, `Unavailable` and unexpected failures return 409, 503 and 500 instead of 400.
  - 500 responses never include internal error messages.

- The `BackgroundJobs` configuration section is now `JobScheduling` (`Enabled`, `RunServer`, `WorkerCount`), and the Hangfire dashboard moved from `/jobs` to `/hangfire`.
- `Newtonsoft.Json` is pinned to 13.0.4 (Hangfire.Core only requires 11.0.1, which has advisory GHSA-5crp-9r3c-p9vr).
- A root `.gitattributes` enforces LF line endings repository-wide.

- `POST /users` was removed (users now register in Keycloak), every `/users` endpoint requires authentication, and `User` has an `ExternalId` (Keycloak `sub`) in place of a password.
- The Angular dev server's `proxy.conf.ts` was removed; the backend for frontend is now the single browser origin.

- Template license set to MIT (the source project it was based on used AGPL-3.0, which is unsuitable for a reusable starting point).

[Unreleased]: https://github.com/andregabrielmelo/dotnet-angular-aspire-template/commits/main
