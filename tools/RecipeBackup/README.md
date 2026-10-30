# Database and referenced-image recovery tool

Implements the portable export/restore foundation for [issue #55](https://github.com/lhjenner/recipe-app-monorepo/issues/55).
No Neon account changes, GitHub Secrets, recovery key, or real-data import are
needed for the automated synthetic rehearsal.

## What is implemented

- One custom-format PostgreSQL dump containing the application database,
  including EF migration history, accounts, sessions, recipes, and image associations.
- Referenced private image objects, including archived recipes; unreferenced
  bucket objects are **not** included.
- A versioned manifest recording recipe/owner IDs, opaque object keys, image
  metadata, byte lengths, and SHA-256 checksums. Identical image content is
  stored once, but every distinct referenced source key is checked.
- Full archive verification before publication and before restore writes.
  Missing/corrupt objects, unexpected/duplicate ZIP entries, unsupported
  manifests, and checksum mismatches fail the operation.
- Restore to an empty isolated database and bucket, read-back verification of
  every uploaded key, and comparison of restored associations with the manifest.
- Archive size output, including six-copy storage and the temporary seven-copy
  peak needed when uploading a replacement before deleting old backups.

The [manual-only workflow](../../.github/workflows/backup.yml) encrypts the
verified archive before uploading, verifies a re-downloaded copy, and retains
up to six successful backups with 42-day expiration. It fails closed until
repository configuration is supplied. See [automation setup and recovery](AUTOMATION.md).
Do not mark recovery acceptance criteria complete until a live encrypted backup
and real-data restore rehearsal pass.

## Prerequisites and confidentiality

Install .NET 10 and PostgreSQL client tools (`pg_dump` and `pg_restore`) on PATH.
Use client major version 18 for the current Neon PostgreSQL 18 database; restore
to the same PostgreSQL major version for the first rehearsal. The tools must be
able to connect directly to the database, not through PgBouncer.

These are **plaintext archives containing private data**, including password
hashes and session records. Never commit, attach to issues, or upload them as
Actions artifacts. Use a private local folder under ignored `backups/` for
manual work. ZIP compression and SHA-256 are not encryption or authentication.
Only restore trusted archives: PostgreSQL dumps contain executable database
definitions. Successful checksum verification alone does not establish trust.

Temporary dump/image files are removed on normal success, failure, or
cancellation. Unix files are created owner-read/write only; Windows uses the
current user's directory ACLs. Use an access-restricted temporary directory,
adequate disk space, and an ephemeral runner for future automation. A crash or
forced process termination can leave sensitive temporary files or `.partial`
output behind; inspect and remove those specific files before reusing a runner.
Ordinary deletion is not guaranteed secure erasure.

Limits: 100,000 image associations, 16 MiB manifest, 2 GiB per data entry, and
8 GiB total uncompressed archive data including the manifest. These are tool
safety ceilings, **not** cloud free-tier allowances. Both input/output archives
and a dump plus one staged image may temporarily occupy disk during restore.

## Manual commands (not needed for the synthetic tests)

The tool reads process environment variables only. It never loads `.env`,
falls back to User Secrets, creates buckets, runs migrations, or provisions
infrastructure. Inject secrets through your environment/secret manager; do not
put connection strings or access keys on the command line.

For export, provide:

| Variable | Purpose |
|---|---|
| `DATABASE_URL_UNPOOLED` | Direct PostgreSQL URL with verified TLS |
| `AWS_ENDPOINT_URL_S3` | Source branch's HTTPS S3 endpoint |
| `AWS_REGION` | Source region |
| `AWS_ACCESS_KEY_ID` | Source storage credential ID |
| `AWS_SECRET_ACCESS_KEY` | Source storage secret |
| `BACKUP_BUCKET` | Explicit source bucket, currently `recipe-images-dev` |

`dotnet run` builds and launches the tool. `--project` selects its project;
the standalone `--` separates .NET options from tool arguments. `export` creates
a new archive and refuses to overwrite an existing output or partial file.
Choose an existing private output directory and a unique filename:

```powershell
dotnet run --project .\tools\RecipeBackup -- export .\backups\manual-recovery.zip
```

`verify` checks the archive without database or storage credentials, performs
no network calls, and reports measured sizes:

```powershell
dotnet run --project .\tools\RecipeBackup -- verify .\backups\manual-recovery.zip
```

For restore, provision a **new empty database and private bucket**. Prefix every
variable in the table with `RESTORE_`, including `RESTORE_BACKUP_BUCKET`.
Set `RESTORE_CONFIRM_EMPTY=yes` only after independently checking the destination.
There is deliberately no fallback to source credentials. Do not run EF migrations
on the empty target first; the dump restores schema and migration history.
Use a role with the privileges needed to recreate the source schema, and ensure
any required PostgreSQL extensions are available.

`restore` verifies the archive, refuses existing user relations or any current
bucket objects, uploads/verifies images, then restores the database in a single
PostgreSQL transaction. It does not drop/truncate existing data:

```powershell
dotnet run --project .\tools\RecipeBackup -- restore .\backups\manual-recovery.zip
```

If anything fails, no success is claimed. Object storage and PostgreSQL do not
share a transaction: uploaded objects, or a restored database if final
association checks fail, may remain. Inspect the isolated destination and retry
with **fresh empty targets**; this tool does not delete partial cloud data.
Keep the destination isolated from application traffic throughout restoration.

The dump omits ownership/ACLs and is not a cluster/global-role export. Recreate
environment configuration, database roles/grants, provider access policies, and
bucket privacy separately. Before cutover, invalidate restored sessions/reset
tokens according to the application's recovery procedure and verify login and
authenticated Recipe View against the restored service.

## Consistency boundary

The exporter opens a read-only repeatable-read transaction, exports its snapshot,
reads all image associations in that snapshot, and passes the same snapshot to
`pg_dump`. The database dump and image manifest therefore describe the same
database state.

An ordinary PostgreSQL snapshot cannot freeze S3 objects. Until the image import/
upload pipeline guarantees immutable, versioned keys and retains old referenced
objects across the backup window, **pause image imports, replacement, and deletion
while exporting**. Every referenced key must remain readable with its recorded
bytes until backup completion. A changed/missing object aborts the backup rather
than silently dropping an image. Do not claim this is an atomic provider snapshot.

## Credential-free synthetic rehearsal

Docker Desktop must be running, with Node.js 22, `age`, and `age-keygen` on PATH.
Testcontainers creates two disposable PostgreSQL
18 containers per test; nothing reads the ignored Neon environment files.
The component tests use the real container `pg_dump`/`pg_restore` binaries through
`docker exec`, not host PostgreSQL tools. Source/target object stores are in-memory
test doubles: these tests prove archive/database recovery logic, **not** live S3
provider compatibility, bucket privacy, or real-data recovery. The full recovery
test also encrypts the synthetic archive using the workflow's encryption module,
decrypts it with a disposable test identity, and restores the decrypted archive.
It never uses your recovery key. Encryption, fail-closed configuration, downloaded
artifact checks, and mocked retention behavior also run through `npm run test:tooling`.

`dotnet test` builds and runs xUnit tests. `--filter` selects only the named backup
test classes; `--logger` with `console;verbosity=detailed` includes the synthetic
archive measurement in the console output. Run these sequentially:

```powershell
dotnet test .\apps\api\tests\RecipeApp.Api.UnitTests --filter FullyQualifiedName~BackupArchiveTests
dotnet test .\apps\api\tests\RecipeApp.Api.ComponentTests --filter FullyQualifiedName~BackupRehearsalTests --logger "console;verbosity=detailed"
```

## Remaining operator and acceptance work

Follow [automation setup](AUTOMATION.md) to configure source Secrets, the public
recipient, consistency acknowledgement, notification preferences, and spending
controls. Run a manual workflow and download/decrypt its artifact before relying
on backups or enabling weekly scheduling. The private key stays outside GitHub.

After real import, rehearse a separate database/private-bucket restore, verify
authenticated Recipe View without the temporary migration downloads, and record
real encrypted size, timings, quota evidence, and results for #55 and #60.

The synthetic rehearsal does not depend on importing Firestore data first.
The final representative-size and real-app recovery evidence does.
