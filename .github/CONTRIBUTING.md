# Contributing

This is a personal template repository - the primary way to "use" it is clicking **Use this template**, not contributing back to it. That said, fixes and improvements to the template itself are welcome.

## Reporting an issue

- Check whether it's actually a problem with the template (missing piece, broken build, outdated doc) rather than something specific to your project after you've renamed and extended it.
- Include enough to reproduce it: what you ran, what you expected, what happened instead.

## Branches and commits

- Branching follows [Gitflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow). Name work branches `feature/<name>` (from `develop`, merged back into `develop`), `release/<version>` or `hotfix/<name>`. Never commit directly to `main` or `develop`.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/), for example `feat(auth): add logout endpoint` or `fix(frontend): handle expired session`.

## Pull requests

- Keep PRs focused on one change - a bug fix, a doc update, a workflow tweak.
- Run `dotnet csharpier format .` (backend) and `npx prettier --write .` (frontend) before committing - CI checks formatting.
- If you're changing something architectural, consider whether it needs an [ADR](../docs/content/architecture-decisions/README.md).

Thanks for taking the time to improve it.
