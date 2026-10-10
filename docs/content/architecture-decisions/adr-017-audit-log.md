---
title: "ADR 017: Audit log with allowlisted entity changes"
weight: 170
---

# ADR 017: Audit log with allowlisted entity changes

## Status
Accepted.

## Context
Operators need to answer "who changed this, when, and what was it before?" and "who tried to do something they're not allowed to?". Application logs are the wrong place for that. They are noisy, rotated, and redacted for a different reason, and they can't be queried per entity. An audit log must also not become a second copy of all personal data, or a place where secrets leak.

## Decision
- **Entity changes, opt-in twice.** An entity implements `IAuditable` (SharedKernel) **and** lists exactly the properties to record in `AddAuditing(audit => audit.Audit<User>(u => u.Name, u => u.Email, u => u.PhoneNumber))`. Nothing outside the allowlist is ever serialized. An update that changes none of the allowlisted properties writes no entry (no noise from `WelcomeEmailSentAtUtc` and the like).
- **Same transaction.** `AuditInterceptor` adds `audit_entries` rows to the `SaveChanges` that makes the change. If the audit row can't be written, the change fails too: there is no unaudited change. This is deliberate, and `FailingAuditWriteTests` proves it.
- **Masking.** A recorded property whose property or type is classified (`[PersonalData]`/`[SecretData]`, the same classifications the logs redact) is recorded as changed, with `[redacted]` as its old and new value. `EmailAddress` and `PhoneNumber` are, so the log shows *that* an email changed, not to what.
- **Diffs:** insert: new values; update: only the changed properties, old and new; delete: old values.
- **Actor:**
  - `user:{sub}` when a signed-in user's request made the change (`ICurrentUser`)
  - `system:{job id}` inside a recurring job (`RecurringJobRunner` sets it), `system:outbox` in outbox handlers
  - `anonymous` otherwise
- **Trace id:** stored with each entry, linking it to the request's logs and trace.
- **Explicit events:** `IAuditLog.RecordAsync(action, target, outcome)` records actions that aren't entity changes. Each is saved on its own DbContext, so a failed command's tracked changes are never committed by its audit record.
  - every job management mutation (trigger, pause, resume, remove, restore) and outbox dead-letter requeue: the command implements `IAuditedCommand`, and `AuditingBehavior` (Mediator pipeline) records a returned `Result` as succeeded or failed, and an exception as failed before rethrowing it. Audit is mandatory: a failed write fails an otherwise successful command; after a throw, the command's exception wins and the audit failure is logged
  - a signed-in caller refused an `/v1/admin` endpoint (403), recorded by `AuditingAuthorizationResultHandler` with the route pattern, never the raw path
- **Querying:** `GET /v1/admin/audit`, permission `audit:read` (added to `Permission` and the realm's `admin` role).
  - Filters only on indexed columns (entity type and key, actor), always within a time range: the last 7 days unless `from`/`to` are given.
  - At most 100 entries per page, newest first.
  - Never output-cached.
- **Retention:** `AuditRetentionJob` (daily) deletes entries older than `Audit:RetentionDays` (default 365).
- **Opt-out:** `Audit:Enabled=false` registers a no-op `IAuditLog` and nothing else records. `AuditingDisabledTests` shows the app works that way.

## Consequences
- The log is a **durable security record only as far as retention allows**. Set `Audit:RetentionDays` to what your obligations require, and remember that anyone with database access can still edit rows. For tamper evidence, ship entries to append-only storage as well.
- **Deletion requests** (for example GDPR erasure) are compatible, because personal values are masked. The entity key and actor (`user:{sub}`) remain. Decide whether `sub` itself must be pseudonymized for your case. **Export** is the query endpoint, paged.
- **Row ids are reused** after a delete (they're max+1), so entries for an entity key may belong to an earlier entity with the same id. Filter by time or actor when it matters.
- A **multi-tenant** application needs a tenant column, set from the tenant context and part of every index and query filter.
- Every audited save writes one extra row per changed entity. Bulk updates (`ExecuteUpdate`) bypass the interceptor, so they aren't audited. Use tracked changes for anything that must be.
- The Angular page for browsing the log is a follow-up; the API exists now.
- An existing local Keycloak keeps its imported realm (persistent volume): add the `audit:read` client role and the `admin` composite by hand, or remove the Keycloak volume to re-import.
