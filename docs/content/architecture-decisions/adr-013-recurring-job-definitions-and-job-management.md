---
title: "ADR 013: Recurring job definitions and job management"
weight: 130
---

# ADR 013: Recurring job definitions and job management

## Status
Accepted. Amends [ADR 012]({{< relref "adr-012-background-jobs-hangfire" >}}).

## Context
ADR 012 introduced Hangfire with two hand-registered jobs. Adding a recurring job meant editing the registration code, and nothing outside Development could see, pause or re-run jobs, because the dashboard is Development-only. The structure of [netrock's Jobs feature](https://github.com/fpindej/netrock/tree/dbc6b49844d9095ef96b6492ae9ba304ea3c3c2c/src/backend/MyProject.Infrastructure/Features/Jobs) solves both, and we adopt it, informed by the [Hangfire documentation](https://docs.hangfire.io/en/latest/getting-started/index.html).

## Decision

**Adopted from netrock**, in `AppTemplate.Infrastructure/Jobs/` (next to `Email/` and `Identity/`, with no `Features/` folder):
- **`IRecurringJobDefinition`** (`JobId`, `CronExpression`, `ExecuteAsync`). Definitions are registered with `AddRecurringJob<T>()` in `AddJobScheduling` and scheduled at startup by `UseJobSchedulingAsync()`, which also maps the Development dashboard at Hangfire's default `/hangfire`. Recurring jobs live in `RecurringJobs/`, fire-and-forget jobs in `FireAndForget/`, and the rest in `Options/`, `Models/`, `Configurations/`, `Services/` and `Extensions/`.
- **One entry point for every recurring job.** Hangfire stores only the job id, and the runner resolves the definition in a fresh DI scope.
- **Pause and resume.** Hangfire has no native pause, so a paused job stays registered with `Cron.Never()` and a `hangfire.paused_jobs` row keeps its schedule.
- **`IJobManagementService`** (UseCases): list, detail with recent runs, trigger, pause, resume, remove, and restore. Restore is a **sync with code**: it re-registers every definition (keeping paused jobs paused) and removes recurring jobs whose definition no longer exists, which startup does too. It is exposed as `/admin/jobs` endpoints:
  - reading needs `jobs:read`; changing needs `jobs:manage`
  - changes are rate limited per client IP
  - the Angular pages are `/jobs` and `/jobs/:jobId`
- **`JobScheduling` options:** `Enabled` (when false, Hangfire isn't registered and background work is logged and skipped) and `WorkerCount`, plus our `RunServer`, which separates enqueueing from processing.

**Deliberate deviations from netrock:**

| netrock | here | why |
|---|---|---|
| Static root `IServiceProvider` captured at startup, used by a static `ExecuteJobAsync(jobId)` | `RecurringJobRunner`, a DI-activated class that Hangfire gives its own scope per run | Static state is process-global. With several hosts in one process (as in tests), jobs would run in whichever host set it last. Hangfire's own static logger already caused flaky tests here (ADR 012). |
| Static `PausedJobCrons` dictionary cached from the database | The `paused_jobs` table is read every time | A per-process cache lets API instances disagree about which jobs are paused. |
| `ExecuteAsync()` with no parameters, and `GetAwaiter().GetResult()` at startup | `ExecuteAsync(CancellationToken)`, and async `UseJobSchedulingAsync` | The Hangfire docs recommend `CancellationToken` parameters (signalled on shutdown). Sync-over-async blocks startup threads. |
| Execution history filtered by job type | Filtered by the job id argument | All recurring jobs share one runner type, so filtering by type would mix their histories. |
| Resume restores the stored cron | Resume restores the definition's current cron (the stored one only if the definition is gone) | A schedule changed in code while a job was paused takes effect. |
| Recurring jobs whose definition was renamed or deleted stay registered forever, firing and doing nothing | Registration (startup and Restore) removes `RecurringJobRunner` jobs that have no definition, along with their pause state. Jobs registered with Hangfire any other way, or whose stored invocation can't load, are left alone | The scheduler always matches the definitions in code. |
| Pause checks for a row, then inserts it; the same pattern for deletes | Losing that race to a concurrent request counts as success; other database errors still propagate | Two admins acting at once shouldn't get a 500 for an operation that did happen. |
| Dashboard `Authorization = []` in Development | `LocalRequestsOnlyAuthorizationFilter` | Hangfire's own default. Anyone who can reach the port shouldn't be able to delete jobs. |

**From the Hangfire docs:**
- storage and managers come from DI
- `Version_180` compatibility with the recommended serializer settings
- small, primitive job arguments, and idempotent jobs
- per-job retry counts chosen deliberately:
  - recurring runs: 2 (the next scheduled run catches up)
  - welcome email: 5, with explicit back-off delays
- runs of the same job never overlap: `[DisableConcurrentExecution("recurring-job:{0}", …)]` locks per job id, confirmed in Hangfire 1.8's source
- nothing request-scoped (such as `ICurrentUser`) is used in jobs, because request information isn't available when a job's class is created

## Consequences
- Adding a recurring job: implement `IRecurringJobDefinition` and register it with `AddRecurringJob<T>()`. It's scheduled on the next startup, and it appears in the admin API and UI.
- **Job ids are contracts**, keyed in Hangfire storage and `paused_jobs`. Never rename a deployed one.
- Pause state and the schedule survive restarts. Startup re-registers every definition (restoring any deleted from the dashboard), with paused ones on `Cron.Never()`.
- `paused_jobs` is created by an EF migration in Hangfire's schema. Migrations must be applied before the API starts, because startup reads that table.
- `Hangfire.Core`'s minimum `Newtonsoft.Json` (11.0.1) has a known vulnerability (GHSA-5crp-9r3c-p9vr), so 13.0.4 is pinned explicitly.
- **Rolling deployments:** registration runs on every startup, so the first instance of a new version removes jobs its code no longer defines. That happens even while instances of the old version are still running and could execute them, which is harmless for jobs that were deliberately deleted.
- Pause, resume and remove are idempotent under concurrency. The unique index on `paused_jobs.job_id` is what makes a concurrent pause safe.
