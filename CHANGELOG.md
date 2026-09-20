# Changelog

All notable changes to **this template** are documented here (not changes to projects started *from* it). Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

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

- A stale Angular test asserting markup that no longer existed.
- Dead `[Required]` Data Annotations on FastEndpoints request DTOs (FastEndpoints validates via FluentValidation, not Data Annotations - these had no effect).
- Missing `.AsNoTracking()` on read-only EF Core specifications.
- A cross-platform line-ending mismatch (`.editorconfig` required CRLF while committed blobs were LF) that passed CI on Windows but failed csharpier's format check on Linux/macOS.

### Changed

- Template license set to MIT (the source project it was based on used AGPL-3.0, which is unsuitable for a reusable starting point).

[Unreleased]: https://github.com/andregabrielmelo/dotnet-angular-aspire-template/commits/main
