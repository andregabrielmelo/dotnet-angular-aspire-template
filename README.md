# dotnet-angular-aspire-template

![Backend Build](https://github.com/andregabrielmelo/dotnet-angular-aspire-template/actions/workflows/backend-build.yml/badge.svg)
![Frontend Build](https://github.com/andregabrielmelo/dotnet-angular-aspire-template/actions/workflows/frontend-build.yml/badge.svg)

A GitHub template repository for starting new full-stack projects with:

- **.NET 10** backend using **Clean Architecture** (Core / UseCases / Infrastructure / Web)
- **FastEndpoints** + **Mediator** for the API (REPR pattern + CQRS)
- **EF Core** + **PostgreSQL** for persistence
- **Angular** frontend
- **.NET Aspire** orchestrating all of the above for local development

It's not a sample app - it's a skeleton with one real feature (`User`) wired end-to-end so you can see where things belong. Replace it with your own domain once you understand the shape.

**📖 Full docs: [andregabrielmelo.github.io/dotnet-angular-aspire-template](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/)**

## Quickstart

1. Click **Use this template** above (or `gh repo create your-project --template andregabrielmelo/dotnet-angular-aspire-template --clone --public`).
2. Rename `AppTemplate` to your project's name:
   ```bash
   scripts/rename-template.sh YourProjectName
   ```
3. Run everything via Aspire:
   ```bash
   cd backend
   dotnet run --project src/YourProjectName.AppHost
   ```

See [Getting Started](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/getting-started/) for prerequisites and details.

## Docs

- [Getting Started](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/getting-started/)
- [Goals & Design Decisions](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/design-decisions/)
- [Architecture Decisions (ADRs)](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/architecture-decisions/)

## License

[MIT](LICENSE)
