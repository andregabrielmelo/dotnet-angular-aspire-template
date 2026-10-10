---
title: "ADR 018: File storage on S3-compatible object storage (Garage locally)"
weight: 180
---

# ADR 018: File storage on S3-compatible object storage (Garage locally)

## Status
Accepted.

## Context
Applications need to store user files (avatars, documents). Keeping them in Postgres bloats the database and its backups. The local filesystem doesn't survive containers or scale past one instance. The S3 API is the common denominator of object stores, whether cloud-hosted or self-hosted.

Uploads are also untrusted input. An image can lie about its type, hide a script, carry GPS coordinates in its metadata, or be a "decompression bomb" whose few kilobytes decode to gigabytes.

## Decision
- **S3 API, Garage locally.** `IFileStorage` (UseCases) has an `S3FileStorage` implementation (Infrastructure, AWSSDK.S3) with path-style addressing, so a single-host service works without per-bucket DNS. Only the flexible checksums S3 requires are sent, since S3-compatible services differ on the rest. The AppHost runs [Garage](https://garagehq.deuxfleurs.fr/) `v2.4.1`, a small Rust S3 server. (Garage over MinIO was a project decision; MinIO is AGPL-3.0.) In production, point `FileStorage:*` at any S3-compatible service.
- **Bootstrap without an admin API.** Garage 2.3+ runs `server --single-node --default-bucket`, which creates a one-node layout plus the access key and bucket from `GARAGE_DEFAULT_*` on first start, and is idempotent on restart. `garage.toml` leaves the `[admin]` section commented out, so there's no admin port or token for the app (or anyone) to leak. The Web API gets only the S3 endpoint, key and bucket; never Garage's RPC secret. The image, command and config live in `AppHost/Garage/` and are linked into the functional tests, which run exactly what developers run.
- **Optional dependency.** Without `FileStorage:ServiceUrl`, an "unavailable" storage is registered and nothing else changes. With it, outages become `FileStorageUnavailableException`, mapped to **503** problem details on file endpoints only, and the `file-storage` health check (tag `dependency`) reports **Degraded** on `/health/dependencies` while `/health` stays healthy.
- **Upload pipeline** (`SkiaImageProcessor`), cheapest check first:
  1. bytes are capped at 2 MB, both by the request size limit and while reading
  2. the **file signature** must be JPEG, PNG or WebP (the client's `Content-Type` and file name are ignored)
  3. dimensions are read **from the header, before decoding**: more than 4096 px on a side or 16 MP is refused, so a bomb never allocates pixels
  4. the image is decoded within a timeout, the EXIF orientation applied, scaled to fit 512×512, and **re-encoded as WebP**, leaving every original byte behind (metadata included)

  SkiaSharp (MIT) was chosen over ImageSharp, whose Six Labors Split License would require a paid license from some of the companies that start projects from this template.
- **Keys are opaque** (`avatars/{guid}`): nothing from the upload or the user, never reused, so the key is a strong ETag.
- **Avatar slice:**
  - `PUT /v1/users/{id}/avatar` (multipart) and `DELETE` are **owner-only**, even for `users:write`; uploads are rate-limited (10 a minute).
  - `GET` serves your own avatar, or anyone's with `users:read`, with `ETag`, `If-None-Match` → 304 (without touching storage), and `Cache-Control: private, no-cache`.
  - The SPA fetches it as a blob, since the backend for frontend needs a CSRF header an `<img src>` can't send. The CSP allows `img-src blob:`.
- **No orphaned objects:** replacing or removing an avatar, or deleting the user, raises `StoredFileOrphaned` in the same transaction. The outbox deletes the object and retries until storage accepts it.
- **Metrics:** `files.uploaded`, `files.upload.size`, `files.rejected` (by reason) and `files.deleted`.

## Consequences
- An object is stored before the database row points at it. If that save fails, the new object is orphaned (rare, and harmless apart from the space). A periodic sweep of unreferenced keys is the remedy if it ever matters.
- Every upload is decoded on the API server. The limits bound memory to about 64 MB per decode, and rate limiting bounds how often. Heavy media workloads belong in a separate worker.
- Other file types (documents) need their own pipeline. The re-encode trick is specific to images; for PDFs and the like, validate the signature, store under an opaque key, and serve with `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`.
- Garage keeps its data in the `apptemplate-garage-data` volume. Remove the volume to start over.

## Removing it
Delete the `AddFileStorage` call, `Infrastructure/Files/`, `UseCases/Files/`, `UseCases/Users/Avatar/` and `Web/Features/UserFeatures/Avatar/`, the `garage` resource and its `FileStorage__*` variables in `AppHost.cs` (plus `AppHost/Garage/`), `User.AvatarKey` and its methods (with a migration dropping the column), the `StoredFileOrphaned` event and its outbox registration, the frontend `AvatarEditor`, and the `files.*` metrics.
