# GitHub Actions Workflows

## `backend-build.yml`

Restores, format-checks (`dotnet csharpier check`), and builds the backend solution on Windows, Linux, and macOS. Triggers on pushes/PRs that touch `backend/**`. There are no test projects yet - see the note at the bottom of the workflow for how to wire one in.

## `frontend-build.yml`

Installs, format-checks (`prettier --check`), tests (`vitest`, via `npm run test`), and builds the Angular app. Triggers on pushes/PRs that touch `frontend/**`.

## `codeql-analysis.yml`

Monthly (and on-demand) security scan of both the C# backend and the TypeScript/Angular frontend using CodeQL's `security-and-quality` query suite. Not run on every push - it's slow and isn't meant to gate PRs.

## `hugo-docs.yml`

Builds the `docs/` Hugo site (using the `hugo-book` theme, fetched fresh each run) and deploys it to GitHub Pages on pushes to `main` that touch `docs/**`. Requires Pages to be enabled for the repo with the source set to "GitHub Actions" (Settings → Pages).

## Adding a status badge

```markdown
![Backend Build](https://github.com/<owner>/<repo>/actions/workflows/backend-build.yml/badge.svg)
![Frontend Build](https://github.com/<owner>/<repo>/actions/workflows/frontend-build.yml/badge.svg)
```
