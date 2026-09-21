# Backend

.NET 10 solution following Clean Architecture (`Core` / `UseCases` / `Infrastructure` / `Web`), orchestrated locally via Aspire.

```bash
dotnet run --project src/AppTemplate.AppHost   # starts Postgres, the API, and the frontend together
dotnet build AppTemplate.slnx
dotnet test AppTemplate.slnx                   # xUnit: AppTemplate.UnitTests + AppTemplate.FunctionalTests
dotnet csharpier check .                       # formatting, enforced in CI
```

New migration:

```bash
dotnet ef migrations add YourMigrationName \
  --project src/AppTemplate.Infrastructure \
  --startup-project src/AppTemplate.Web \
  -o Data/Migrations
```

Authentication (ASP.NET Core Identity + JWT) needs a signing key. A development-only placeholder
ships in `appsettings.Development.json` so `dotnet run` works out of the box; override it locally
without committing anything by running:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<a long random string>" --project src/AppTemplate.Web
```

In any shared or production environment, set the `Jwt__SigningKey` environment variable instead -
the app throws on startup if no signing key is configured outside Development.

See the [docs site](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/) for the architecture, design decisions, and ADRs - start with [Getting Started](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/getting-started/).
