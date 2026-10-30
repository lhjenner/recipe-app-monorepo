# Encrypted development backups

Tracks the automated recovery portion of [issue #55](https://github.com/lhjenner/recipe-app-monorepo/issues/55).
The [workflow](../../.github/workflows/backup.yml) is **manual-trigger only**.
No scheduled runs are enabled while source configuration and live recovery
verification are outstanding. After a successful live backup/decrypt/restore
rehearsal, enable the agreed weekly schedule with cron `17 3 * * 0`: Sundays at
**03:17 UTC** (16:17 NZDT / 15:17 NZST). GitHub schedules can be delayed; they are
not exact-time guarantees. The minute avoids the busy top of the hour.

Current imported ingredients remain in local development PostgreSQL. This
workflow does not back up that local database, migrate it to Neon, or make Neon
the application database. Keep the local data protected separately. Choose and
document the database destination as part of migration rehearsal rather than
moving real data solely to enable this workflow.

This backs up the **development** database and private `recipe-images-dev`
bucket on the same Neon branch. The currently linked branch happens to be
named `production`, but is used for development. It is not production hosting
or a production backup policy. A separate production bucket/credential set,
workflow namespace, and explicit go-live transfer are still required.

## What the workflow does

1. Reject missing configuration, non-default Git branches without manual test opt-in, a different bucket,
   an invalid public recipient, or an unacknowledged image consistency policy.
2. Install PostgreSQL 18 client tools and `age`, and build the existing .NET tool.
3. Export/verify the database and referenced objects into a private, unique
   temporary directory. Native dump credentials are passed through environment
   variables, never command-line URLs. Source credentials exist only in the
   preflight and export steps, not dependency installation, build, or retention.
   `PGSSLROOTCERT=system` makes the PostgreSQL 18 native client use the runner's
   installed CA trust store with full certificate/hostname verification, rather
   than requiring a separate `~/.postgresql/root.crt` file.
4. Encrypt to the configured public recipient. The public recipient and `age`
   executable are tested before reading source data. There is no plaintext
   upload fallback. Export/encryption/size failures fail the job.
5. Upload exactly `recovery.zip.age` as an artifact named
   `recipe-backup-dev-<run ID>-<attempt>`, with no additional compression and a
   42-day expiration. Reject encrypted files larger than **60 MiB** before upload.
6. In a separate job with no source credentials, re-download the artifact and
   require exactly the expected filename, age header, length, and SHA-256.
   The workflow cannot test decryption because it never has the private key.
7. Only then, retain this verified replacement plus the five newest successful
   previous attempts from this workflow/default branch. Remove older copies and
   completed failed/cancelled attempts in this namespace. Unrelated artifacts
   and other active runs are not deleted. API lookup failures prevent deletions;
   delete failures are explicit job failures.
8. Remove local staging files even if a step fails or is cancelled, where GitHub
   still permits cleanup execution. An ephemeral runner is required; forced
   termination cannot guarantee cleanup. Ordinary deletion is not secure erasure.

Concurrent backups are serialized. GitHub may replace a queued pending run with
a newer pending run; it does not cancel the active backup. A manual rerun gets a
new artifact name when export is rerun. Only the latest uploaded artifact of each
successful run counts as a prior successful backup, not superseded earlier
attempts. Rerunning failed verification jobs can reuse an existing upload.

The retention job has `actions: write` for deleting artifacts. Export has only
`contents: read`; verification has `contents: read` and `actions: read`. Nothing
commits or pushes backup files, provisions cloud resources, or modifies source
database rows/objects. No pull-request trigger has access to backup credentials.

## Configure before relying on backups

### Iterating safely on a trusted feature branch

Keep normal CI enabled. Since this workflow already exists on the default branch,
you can manually dispatch the version on a pushed feature branch without merging
each correction. Select that branch and explicitly enable **Test this trusted
branch**. Until this input reaches main, dispatch the branch version with the
GitHub CLI instead of relying on the default branch's older form.

Only run reviewed code from a branch you trust: it uses the real development
source Secrets. Do not add a PR trigger or use `pull_request_target` to execute
PR code with Secrets. PR events and tags are rejected by run-mode validation.

Test artifacts use `recipe-backup-test-<run ID>-<attempt>` and expire after seven
days. Verification still downloads and checksum-checks the encrypted file, but
the retention/deletion job is skipped entirely. Test artifacts neither replace
nor count toward the six default-branch backups. Repeated successful tests use
additional shared storage until manually deleted or expired; remove unneeded
test artifacts after debugging. No schedule is added.

For ordinary default-branch backups, leave the test option unchecked. The six-copy,
42-day policy remains unchanged. A successful branch test proves export/encryption
and artifact download, not the skipped live retention API calls or restore.

Commit/merge the reviewed workflow and scripts to the default branch for manual
activation. Future schedules also require the workflow there. Do not copy recovery private keys into
GitHub, the repository, environment files, or chat.

In **Repository Settings > Secrets and variables > Actions**, add:

### Repository Secrets

| Secret | Value source |
|---|---|
| `BACKUP_DATABASE_URL_UNPOOLED` | Development branch's direct PostgreSQL URL, not `-pooler` |
| `BACKUP_AWS_ENDPOINT_URL_S3` | Same branch's HTTPS S3 endpoint |
| `BACKUP_AWS_ACCESS_KEY_ID` | Same branch's source storage credential ID |
| `BACKUP_AWS_SECRET_ACCESS_KEY` | Corresponding source storage secret |

Use the least privileges supported by the provider: database export needs read
access to all dumped application tables/sequences; source storage needs object
reads. Do not add a Neon management API key or restore credentials.
Do not post secret values in issues or workflow logs.

### Repository Variables

| Variable | Value |
|---|---|
| `BACKUP_AGE_RECIPIENT` | Your verified public recipient beginning `age1`; one recipient only |
| `BACKUP_AWS_REGION` | `ap-southeast-1` for the current Singapore development project |
| `BACKUP_BUCKET` | `recipe-images-dev` |
| `BACKUP_IMAGE_CONSISTENCY_CONFIRMED` | `true`, only after satisfying the policy below |

**Consistency acknowledgement:** the PostgreSQL dump and image associations
share a snapshot, but S3 is not frozen by that snapshot. Before setting the final
variable, ensure image objects are immutable/versioned and retained throughout
backup, or enforce a window with no image imports/replacements/deletions.
The current app has no image authoring path; do not run manual image imports or
bucket edits during backup. This acknowledgement records an operator obligation;
it is not a storage lock. Reassess it when #57 adds imports or photo editing is
introduced. Referenced missing/changed bytes fail the export.

### Billing and notification checks

- Check the repository owner's **Billing and licensing** usage, budget, and
  spending controls. GitHub Free currently includes 500 MB of shared
  Actions-artifact/Packages storage and 2,000 standard runner minutes monthly
  for private repositories. Other repositories consume the same account pool.
- At 50 MiB per archive, six copies occupy 300 MiB, and replacement briefly
  needs about 350 MiB. The 60 MiB upload cap bounds seven ciphertext files at
  420 MiB before GitHub's small ZIP-wrapper overhead, **not**
  all account usage. Existing failed artifacts may remain until a successful
  replacement or expiration, so this is not an absolute account storage ceiling.
- Artifacts are separate from Git repository storage. Storage is metered over
  time; runner time includes installation, export, object requests, encryption,
  upload, re-download, and cleanup. Source egress also uses Neon's allowance.
- Repository/organisation retention settings must permit 42 days. Count and age
  are independent: missed runs can leave fewer than six copies or eventually none.
- Enable personal GitHub **Actions notifications** for failed workflows and
  verify the notification destination. The workflow adds failure annotations
  and summaries; email delivery depends on your settings. An entirely missed
  scheduled run cannot send a failure notification, so periodically check backup
  freshness. Weekly backups can lose a week's changes, or more after missed runs.

Sources: [Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions),
[artifact retention](https://docs.github.com/en/actions/using-workflows/storing-workflow-data-as-artifacts),
[age](https://github.com/FiloSottile/age).
Provider quota/exhaustion evidence and actual account spending controls remain
acceptance checks, not guarantees made by this workflow.

## First live run and recovery check

1. Open **Actions > Encrypted development backup > Run workflow** and select
   the default branch. Confirm all jobs pass, including verification/retention.
2. Record elapsed time, encrypted size, SHA-256, and account storage use from
   the run summary/billing view. The workflow summary does not contain secrets.
3. Download the run's artifact. GitHub wraps it in a ZIP: extract it locally
   into an access-restricted folder outside Git, such as ignored `backups/`.
   It should contain only `recovery.zip.age`.
4. Compare the encrypted file's SHA-256 with the producing run's summary.
   `Get-FileHash` reads a file without changing it; `-LiteralPath` selects the
   exact filename and `-Algorithm SHA256` selects the checksum algorithm:

   ```powershell
   Get-FileHash -LiteralPath .\backups\recovery.zip.age -Algorithm SHA256
   ```

5. Retrieve your private identity from the password manager locally. `age
   --decrypt` selects decryption, `-i` selects that local identity, `-o` names
   the decrypted output, and the final argument is the ciphertext input.
   Choose a new output filename: `age` overwrites existing output files.

   ```powershell
   age --decrypt -i "$env:APPDATA\RecipeAppRecovery\recovered-key.txt" -o .\backups\downloaded-recovery.zip .\backups\recovery.zip.age
   ```

6. `dotnet run --project` selects the recovery tool, `--` separates its
   arguments, and `verify` checks the archive without contacting services:

   ```powershell
   dotnet run --project .\tools\RecipeBackup -- verify .\backups\downloaded-recovery.zip
   ```

7. Follow [empty-target restore](README.md#manual-commands-not-needed-for-the-synthetic-tests)
   using separate credentials. Never test restore against your source branch.
   Check accounts/recipes, every referenced object's checksum, and authenticated
   Recipe View once that UI/import is available. Restore roles, bucket privacy,
   settings, and session invalidation separately as described in the recovery guide.
8. Remove the extra local private-key copy and sensitive plaintext staging after
   the rehearsal, retaining the durable password-manager identity. Record the
   results for #55; repeat against representative data under #60.

If you rotate the recipient, keep old private keys until every retained backup
encrypted to them has expired or been deliberately retired. Losing an identity
permanently loses those backups. Compromise of an identity exposes corresponding
downloadable backups; revoke access and rotate according to your incident plan.

## Local validation, without cloud credentials

Install Node.js 22, .NET 10, Docker, `age`, and `age-keygen`.
`npm run test:tooling` runs Node's built-in tests for configuration, actual age
encryption/decryption with disposable keys, cleanup, download checks, and mocked
GitHub retention APIs. The PostgreSQL component rehearsal also encrypts/decrypts
a real synthetic database/image archive before restoring it. CI runs both.
Neither test suite uses live Neon credentials or your recovery identity.

Workflow syntax was checked with `actionlint`; its optional shellcheck integration
is a separate validation. A passing local test is not evidence that GitHub Secrets,
live provider permissions, artifact APIs, scheduled delivery, or real-data recovery
have been exercised. Keep #55 open until the required operator evidence exists.

## Diagnosing a failed export/encryption step

Logs identify recipient validation, database/image export, and archive encryption
separately. The .NET export's credential-safe diagnostics are preserved; raw age
and provider command diagnostics remain suppressed. Never print environment
variables or connection strings to investigate a failure. A generic export error
does not establish that the database is empty or that credentials are incorrect.
