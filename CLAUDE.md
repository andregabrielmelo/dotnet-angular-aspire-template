# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A GitHub **template repository** for starting new full-stack projects: a Clean Architecture .NET backend, an Angular frontend, and .NET Aspire orchestrating both locally. It is not a sample app - the `User` feature is a deliberate end-to-end reference slice (Core → UseCases → Infrastructure → Web) to copy the shape of when adding a real feature, then delete once no longer needed as a reference.

Every project, namespace, folder, the Postgres database name, and the Keycloak realm/client names are currently named `AppTemplate` / `apptemplate`. When someone actually uses this template for a new project, they run `scripts/rename-template.sh YourProjectName` (a plain find-and-replace, not a `dotnet new` template engine) as their first step - keep that in mind if a task looks like it wants a "real" project name; the placeholder is intentional.

Full documentation (architecture, design decisions, ADRs) lives in `docs/` (a Hugo site) and is authoritative for *why* things are built this way - read it before making architectural changes: `docs/content/design-decisions.md` and `docs/content/architecture-decisions/adr-*.md`.

## Commands

All backend commands run from `backend/`; all frontend commands from `frontend/`.

### Backend (.NET 10)

```bash
dotnet run --project src/AppTemplate.AppHost      # runs Postgres + API + frontend together via Aspire
dotnet build AppTemplate.slnx
dotnet test AppTemplate.slnx                      # all tests
dotnet test tests/AppTemplate.UnitTests/AppTemplate.UnitTests.csproj --filter "FullyQualifiedName~UserTests"  # single class/test
dotnet csharpier check .                          # format check (CI-enforced); `format .` to fix
```

New EF Core migration:

```bash
dotnet ef migrations add YourMigrationName \
  --project src/AppTemplate.Infrastructure \
  --startup-project src/AppTemplate.Web \
  -o Data/Migrations
```

Migrations apply automatically on startup only in the `Development` environment (`DatabaseConfigurations.StartDatabase`, called from `Program.cs`); elsewhere they're a no-op unless `Database:ApplyMigrationsOnStartup` is set.

A git pre-commit hook (Husky.Net, see `.husky/`) runs `dotnet csharpier format` on staged `.cs` files automatically. `Directory.Build.targets` at the repo root installs it the first time anyone runs `dotnet build`/`dotnet run`/`dotnet test` after cloning - no manual setup step. Set `HUSKY=0` to skip it (already set in CI, which never commits).

### Frontend (Angular)

```bash
npm ci
npm start                                          # ng serve - reached through the backend for frontend, not directly
npm run build
npm run test                                       # vitest, runs once (not watch mode)
npm run lint                                       # eslint (@angular-eslint), CI-enforced
npx prettier --check "src/**/*.{ts,html,css}"      # format check (CI-enforced); --write to fix
```

### Docs site (Hugo, in `docs/`)

Not run day-to-day, but useful when editing docs: requires the `hugo-book` theme, which CI clones fresh each run rather than vendoring it. To build locally: `git clone --depth 1 https://github.com/alex-shpak/hugo-book themes/hugo-book` inside `docs/`, then `hugo --minify`. Don't commit `docs/public/`, `docs/themes/`, or `docs/resources/` (gitignored at the repo root).

## Architecture

### Backend: Clean Architecture, five projects under `backend/src/` (plus AppHost, ServiceDefaults and BackendForFrontend)

Dependencies point inward; nothing below depends on something above it in this list:

- **`AppTemplate.SharedKernel`** - base types shared by every layer: `EntityBase`/`EntityBase<TId>`, domain event interfaces + dispatch, the Mediator `LoggingBehavior` pipeline behavior. Kept in-repo (not a separate NuGet package) since this template is meant to be self-contained.
- **`AppTemplate.Core`** - the domain model: aggregates (`Aggregates/UserAggregate/`), value objects, specifications, interfaces Infrastructure implements. Near-zero external dependencies (`Ardalis.Specification`, `Vogen`).
- **`AppTemplate.UseCases`** - CQRS commands/queries dispatched via `Mediator` (martinothamar/Mediator, source-generated - **not** MediatR). Depends on Core only; data access goes through `IRepository<T>` (`SharedKernel/IRepository.cs`, an `Ardalis.Specification` repository) and query-service interfaces defined here, implemented in Infrastructure.
- **`AppTemplate.Infrastructure`** - EF Core + Npgsql (`Data/ApplicationDatabaseContext.cs`), email (MailKit), repository/query-service implementations. Anything talking to the outside world lives here.
- **`AppTemplate.Web`** - the ASP.NET Core entry point and composition root (`Program.cs`, `Configurations/`). One [FastEndpoints](https://fast-endpoints.com/) class per endpoint under `Features/<Feature>Features/`, following the REPR pattern (Request-Endpoint-Response), with FluentValidation validators colocated in the same file as their endpoint.

**`AppTemplate.BackendForFrontend`** sits outside the layers: it references only ServiceDefaults and is the browser's single origin. It runs the OpenID Connect flow against Keycloak, keeps the session in an HTTP-only `__Host-apptemplate` cookie, and proxies `/api/**` to Web with YARP, adding the user's access token as `Authorization: Bearer` (Duende.AccessTokenManagement refreshes it). Its session endpoints are `/backend-for-frontend/{login,register,user,logout}`. `/api/**` and `/backend-for-frontend/user` require the header `X-CSRF: 1`.

### Authentication

Keycloak is the OpenID Connect provider and owns credentials, registration and logout pages. The realm is `AppHost/Realms/apptemplate-realm.json`, imported on first start. Web accepts only Keycloak JWTs with audience `apptemplate-api` (`Configurations/AuthenticationConfigurations.cs`, `MapInboundClaims = false`, so read `sub`/`email`/`name`). FastEndpoints endpoints are authenticated unless marked `AllowAnonymous()`. Domain `User` rows are provisioned just in time by `GET /users/me`, linked through `User.ExternalId` (= `sub`). Functional tests authenticate with `TestAuthHandler` via the `X-Test-User` header (`factory.CreateAuthenticatedClient(sub)`). Keycloak's admin console is on `http://localhost:8080`; the admin password is the `keycloak-password` parameter in the Aspire dashboard. See ADR 007.

Password reset (ADR 008) is `POST /password-reset`: anonymous, throttled, and always 202. It goes through `IPasswordResetService` (UseCases). `Infrastructure/Identity/KeycloakPasswordResetService` implements it by calling Keycloak's Admin API `execute-actions-email` (`UPDATE_PASSWORD`) as the `apptemplate-user-admin` service account, using client credentials via Duende.AccessTokenManagement and settings from `KeycloakAdminOptions` (`Keycloak:Admin`). Functional tests replace it with `FakePasswordResetService` (`factory.PasswordResetService`). The backend for frontend proxies only `POST /api/password-reset` anonymously. Keycloak sends its emails to the Mailpit container; read them in Mailpit's web UI.

The realm file is imported only while Keycloak's data volume is empty. After changing `apptemplate-realm.json`, delete that volume, or apply the change in the admin console.

Two test projects under `backend/tests/`, both xUnit (see ADR 006 for the reasoning):

- **`AppTemplate.UnitTests`** - Core/UseCases in isolation, `IRepository<T>` substituted with NSubstitute. No I/O.
- **`AppTemplate.FunctionalTests`** - full HTTP → FastEndpoints → Mediator → EF Core pipeline via `WebApplicationFactory<Program>`. `AppTemplateWebApplicationFactory` swaps the real Postgres `DbContext` for EF Core's InMemory provider so it needs no database/Docker. Swapping it requires stripping **every** `Microsoft.EntityFrameworkCore`-namespaced service from DI, not just `DbContextOptions<T>` - removing only that leaves EF Core's internal per-provider registrations behind and both Npgsql and InMemory end up registered at once ("Only a single database provider can be registered").

Strongly-typed IDs and simple domain primitives (`UserId`, `UserName`) use [Vogen](https://github.com/SteveDunn/Vogen) source-generated value objects with a `Validate` method enforcing invariants at construction. EF Core conversions for them are registered centrally in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs` - add new value objects there, not per-entity. New entity IDs also need a `HasValueGenerator<VogenIdValueGenerator<...>>()` call in that entity's `IEntityTypeConfiguration` (see `UserConfiguration.cs`).

Postgres uses `EFCore.NamingConventions`' snake_case convention, so raw SQL (see `ListUsersQueryService`'s `FromSqlRaw`) must use snake_case column names, not the C# property names.

### Aspire orchestration (`backend/src/AppTemplate.AppHost/AppHost.cs`)

The AppHost wires up these resources for local dev only (not a production deployment mechanism):
- a containerized Postgres with a persistent data volume
- Keycloak on the fixed port 8080, with the realm imported
- Mailpit, a development SMTP catcher for Keycloak's emails
- the Web API
- the backend for frontend on the fixed port `https://localhost:7100`; the realm's redirect URIs depend on this port
- the Angular frontend, via Aspire's JavaScript app hosting (`AddJavaScriptApp` + `WithNpm`, running `npm ci`/`npm start`)

Open the app at `https://localhost:7100`. Connection strings and service URLs are wired by Aspire (`WithReference`/`WaitFor`), not hardcoded in `appsettings.json`.

### Frontend (`frontend/src/app/`)

Standalone Angular components, organized as `core/`, `shared/`, and `features/<feature>/` (currently `auth`, `home`). It holds no tokens:
- `AuthService` asks `/backend-for-frontend/user` whether a session exists.
- Login, register and logout are full-page redirects (`BROWSER_REDIRECT`).
- `core/auth/auth-interceptor.ts` adds `X-CSRF: 1`.
- `authGuard` protects routes.

Relative `api/...` URLs reach the Web API through the backend for frontend. There is no `proxy.conf.ts` and no CORS.

### CI (`.github/workflows/`)

`backend-build.yml` and `frontend-build.yml` both run format-check → (frontend also lints) → build/install → test on every push/PR touching their respective directory (backend across a Windows/Linux/macOS matrix). `codeql-analysis.yml` runs monthly, not per-push. `hugo-docs.yml` deploys `docs/` to GitHub Pages on pushes to `main`. See `.github/workflows/README.md` for details on each.

Line endings are LF throughout (`backend/.gitattributes` pins `eol=lf`, `backend/.editorconfig` sets `end_of_line = lf`) - this was deliberately fixed from an original CRLF setup that passed on Windows runners but broke csharpier's format check on Linux/macOS.

## Git workflow

### Branching: Gitflow

This project follows [Gitflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow):

- `main`: released, production-ready history only. Never commit to it directly.
- `develop`: the integration branch for the next release.
- `feature/<short-kebab-name>` (e.g. `feature/password-reset`): new work. Branch it from `develop` and open its PR back into `develop`.
- `release/<version>`: branched from `develop` to stabilize a release. Merge it into `main` (tagged) and back into `develop`.
- `hotfix/<short-name>`: branched from `main` for urgent production fixes. Merge it into `main` (tagged) and back into `develop`.

Never commit on `main` or `develop` directly. Start a correctly-prefixed branch first. Until a `develop` branch exists on the remote, feature branches start from and target `main`.

### Commits: Conventional Commits

Every commit message follows [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

- Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
- Scopes are optional and name the area touched: `backend`, `frontend`, `auth`, `apphost`, `docs`, `ci`, and so on.
- The description is imperative, lowercase, and has no trailing period.
- Mark breaking changes with `!` after the type/scope and a `BREAKING CHANGE:` footer.
- Prefer several small, focused commits, each of which builds, over one large commit.

