# GitHub Actions Workflows

## `backend-build.yml`

Restores (`--locked-mode`, against the committed `packages.lock.json` files), format-checks (`dotnet csharpier check`), builds, and tests (`dotnet test`) the backend solution on Windows, Linux, and macOS. NuGet packages are cached by `setup-dotnet`, keyed on the lock files' hash. Triggers on pushes to `main`/`develop` and on PRs that touch `backend/**`. `AppTemplate.UnitTests` runs against Core/UseCases with no external dependencies; `AppTemplate.FunctionalTests` exercises the full HTTP -> FastEndpoints -> Mediator -> EF Core pipeline against a real Postgres started with [Testcontainers](https://dotnet.testcontainers.org/). Hosted Windows and macOS runners can't run Linux containers, so only the Linux job runs the whole suite; the others skip tests marked `[Trait("Category", "RequiresDocker")]` (`--filter "Category!=RequiresDocker"`).

## `frontend-build.yml`

Installs, format-checks (`prettier --check`), lints (`eslint`, via `@angular-eslint`), tests (`vitest`, via `npm run test`), and builds the Angular app. Triggers on pushes to `main`/`develop` and on PRs that touch `frontend/**`.

## `pr-conventions.yml`

Runs on every PR and enforces the [Git workflow rules](../CONTRIBUTING.md#branching-gitflow) in plain shell (no third-party actions):
- **Gitflow branch:** `feature/*` may only target `develop`. Only `release/*` and `hotfix/*` may target `main`.
- **Conventional Commits:** every non-merge commit in the PR must match `<type>[(scope)][!]: <description>`.

## `codeql-analysis.yml`

Monthly (and on-demand) security scan of both the C# backend and the TypeScript/Angular frontend using CodeQL's `security-and-quality` query suite. Not run on every push - it's slow and isn't meant to gate PRs.

## `hugo-docs.yml`

Builds the `docs/` Hugo site (using the `hugo-book` theme, fetched fresh each run) and deploys it to GitHub Pages on pushes to `main` that touch `docs/**`. Requires Pages to be enabled for the repo with the source set to "GitHub Actions" (Settings → Pages).

## Conventions shared by every workflow

- **Triggers:** build workflows run on `push` to the two long-lived [gitflow](../CONTRIBUTING.md#branching-gitflow) branches (`main`, `develop`) and on every `pull_request`, not on pushes to every branch. That way a `feature/*` branch with an open PR runs once, not twice. Use `workflow_dispatch` (the "Run workflow" button) to build a branch that has no PR yet.
- **Concurrency:** build workflows cancel an in-progress run when a newer commit lands on the same PR. Runs on `main` and `develop` are never cancelled, so their status history stays complete.
- **Least-privilege token:** every workflow declares `permissions:` explicitly. Build workflows only get `contents: read`.
- **Pinned actions:** every `uses:` points at a full commit SHA with the version as a trailing comment (e.g. `actions/checkout@<sha> # v7.0.1`). Tags are mutable and SHAs aren't. Dependabot (`.github/dependabot.yml`, `github-actions` ecosystem, grouped into one PR against `develop`) keeps both the SHA and the comment current, so don't hand-edit one without the other.
- **`persist-credentials: false`** on every checkout, since no workflow pushes back to the repo. Without it, the token stays in `.git/config` for later steps.
- **`timeout-minutes`** on every job, so a hung job doesn't burn the 6-hour default.

## NuGet lock files

`backend/Directory.Build.props` turns on `RestorePackagesWithLockFile`, so each project has a committed `packages.lock.json`. After changing a package version in `Directory.Packages.props`, run `dotnet restore` and commit the updated lock files. Otherwise CI's `--locked-mode` restore fails. `AppTemplate.AppHost` opts out because the Aspire SDK pulls OS-specific packages (`*.linux-x64`, `*.win-x64`, ...), so its lock file would only ever match the OS that generated it.

## Adding a status badge

```markdown
![Backend Build](https://github.com/<owner>/<repo>/actions/workflows/backend-build.yml/badge.svg)
![Frontend Build](https://github.com/<owner>/<repo>/actions/workflows/frontend-build.yml/badge.svg)
```
