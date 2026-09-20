# Frontend

The Angular app. Normally you run the whole stack via the Aspire AppHost (see the [root README](../README.md)), which starts this alongside the API and database - you shouldn't need to run these commands directly outside of quick iteration.

```bash
npm ci
npm start      # ng serve, proxied to the API via proxy.conf.ts
npm run build  # production build, output in dist/
npm run test   # vitest
```

Formatting is enforced by prettier (`npx prettier --check "src/**/*.{ts,html,css}"`) in CI - run `npx prettier --write` before committing.

See the [docs site](https://andregabrielmelo.github.io/dotnet-angular-aspire-template/notes/cors-and-proxy/) for the CORS/proxy setup and the rest of the template's architecture.
