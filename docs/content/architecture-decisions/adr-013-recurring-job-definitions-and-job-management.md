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
- **Pause and resume**, done differently from netrock (see below).
- **`IJobManagementService`** (UseCases): list, detail with recent runs, trigger, pause, resume, remove, and restore. Restore is a **sync with code**: it re-registers every definition and removes recurring jobs whose definition no longer exists, which startup does too. Paused jobs stay paused. It is exposed as `/admin/jobs` endpoints:
  - the endpoints go through Mediator, like the rest of the API: the queries `GetJobQuery` and `ListJobsQuery`, and one command per action (`TriggerJobCommand`, `PauseJobCommand`, `ResumeJobCommand`, `RemoveJobCommand`, `RestoreJobsCommand`) in `UseCases/Jobs/`
  - reading needs `jobs:read`; changing needs `jobs:manage`
  - changes are rate limited per client IP
  - the Angular pages are `/jobs` and `/jobs/:jobId`
- **`JobScheduling` options:** `WorkerCount`, plus our `RunServer`, which separates enqueueing from processing, and `PrepareSchema`, for databases where the application may not run DDL. netrock's `Enabled` switch is left out: turning it off silently dropped welcome emails, and `RunServer=false` already covers "don't process jobs here".

**Deliberate deviations from netrock:**

| netrock | here | why |
|---|---|---|
| Static root `IServiceProvider` captured at startup, used by a static `ExecuteJobAsync(jobId)` | `RecurringJobRunner`, a DI-activated class that Hangfire gives its own scope per run | Static state is process-global. With several hosts in one process (as in tests), jobs would run in whichever host set it last. Hangfire's own static logger already caused flaky tests here (ADR 012). |
| Pause rewrites the schedule to `Cron.Never()`, keeps the original in a `paused_jobs` table, and caches it in a static `PausedJobCrons` dictionary | The schedule is never touched. Pause state is a set in Hangfire's storage, and `[SkipWhenPaused]`, a client filter on `RecurringJobRunner`, cancels the runs a paused job would create. This is the approach [Hangfire's author suggests](https://discuss.hangfire.io/t/pause-disable-recurring-job/125) | One store, so there's nothing to keep in sync: a pause racing a resume, or a Hangfire call failing after the database write, can't leave a job that looks active but never fires. Adding to or removing from a set is idempotent. A per-process cache would let API instances disagree. |
| `ExecuteAsync()` with no parameters, and `GetAwaiter().GetResult()` at startup | `ExecuteAsync(CancellationToken)`, and async `UseJobSchedulingAsync` | The Hangfire docs recommend `CancellationToken` parameters (signalled on shutdown). Sync-over-async blocks startup threads. |
| Execution history filtered by job type | Filtered by the job id argument | All recurring jobs share one runner type, so filtering by type would mix their histories. |
| Recurring jobs whose definition was renamed or deleted stay registered forever, firing and doing nothing | Registration (startup and Restore) removes `RecurringJobRunner` jobs that have no definition, along with their pause state. Jobs registered with Hangfire any other way, or whose stored invocation can't load, are left alone | The scheduler always matches the definitions in code. |
| Dashboard `Authorization = []` in Development | `LocalRequestsOnlyAuthorizationFilter` | Hangfire's own default. Anyone who can reach the port shouldn't be able to delete jobs. |

**From the Hangfire docs:**
- storage and managers come from DI
- `Version_180` compatibility with the recommended serializer settings
- small, primitive job arguments, and idempotent jobs
- per-job retry counts chosen deliberately:
  - recurring runs: 2 (the next scheduled run catches up)
  - welcome email: 5, with explicit back-off delays
- queue names decide priority, because Hangfire.PostgreSql fetches queues alphabetically: `critical` (emails) sorts before `default`
- a failed enqueue is caught by the hourly `enqueue-missed-welcome-emails` sweep, which picks users still without a welcome email 2 hours after creation (longer than the email job's retries) and at most 7 days old; `users.created_at_utc` (set by Postgres) makes the window possible
- runs of the same job never overlap: `[DisableConcurrentExecution("recurring-job:{0}", …)]` locks per job id, confirmed in Hangfire 1.8's source
- nothing request-scoped (such as `ICurrentUser`) is used in jobs, because request information isn't available when a job's class is created

## Consequences
- Adding a recurring job: implement `IRecurringJobDefinition` and register it with `AddRecurringJob<T>()`. It's scheduled on the next startup, and it appears in the admin API and UI.
- **Job ids are contracts**, keyed in Hangfire storage and in the paused set. Never rename a deployed one.
- Pause state and the schedule survive restarts. Startup re-registers every definition (restoring any deleted from the dashboard), and paused jobs stay paused because the pause lives in Hangfire's storage, not in the schedule.
- While a job is paused, Hangfire still shows its next occurrence in the dashboard, but no run is created. The admin API reports no next run. Trigger still works while paused: it enqueues a run directly, which the filter doesn't cancel.
- `Hangfire.Core`'s minimum `Newtonsoft.Json` (11.0.1) has a known vulnerability (GHSA-5crp-9r3c-p9vr), so 13.0.4 is pinned explicitly.
- **Rolling deployments:** registration runs on every startup, so the first instance of a new version removes jobs its code no longer defines. That happens even while instances of the old version are still running and could execute them, which is harmless for jobs that were deliberately deleted.
- Pause, resume and remove are idempotent under concurrency, because they are single set operations in Hangfire's storage.
