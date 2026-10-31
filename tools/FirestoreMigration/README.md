# Firestore migration tool

Temporary standalone .NET 10 tool for Ingredient migration under
[PRD03](../../docs/prd/03-ingredient-maintenance.md), [issue #42](https://github.com/lhjenner/recipe-app-monorepo/issues/42),
and the upcoming Recipe migration under [PRD04](../../docs/prd/04-recipe-foundation-and-browsing.md),
[issue #56](https://github.com/lhjenner/recipe-app-monorepo/issues/56).
It reuses the API's `AppDbContext` and configurations; the application does not reference
or register the tool. Ingredient mode remains the default and preserves its existing invocation.
Recipe mode currently performs a read-only preview. It validates source-owner selection,
destination owner and schema, Ingredient references, and exact LegacyId matches. It does
not write database changes or upload images. Referenced images are validated only when an
explicit manifest and backup directory are supplied.

## Prepare the target

Start the persistent local PostgreSQL database using the [root setup guide](../../README.md).
Register the destination account through the app and obtain its GUID from `/api/auth/me`.
Do not substitute an old Firestore user ID.

Apply the API's EF migrations before importing. The tool refuses a missing owner,
pending migrations or model changes; it never creates accounts or upgrades the schema.
In this command, `--project` selects the EF model/migrations and `--startup-project`
selects the API's secret configuration:

```powershell
dotnet ef database update --project apps\api\src\RecipeApp.Api --startup-project apps\api\src\RecipeApp.Api
```

The tool has **separate User Secrets** (`recipe-app-firestore-migration`). Configure
`ConnectionStrings:DefaultConnection` locally, using the same target connection as the API.
Use VS Code's secret editor or `dotnet user-secrets`: `set` stores a key/value outside
the repository, and `--project` selects the tool's secret store. Never commit the
actual value or paste it into a migration report. Alternatively inject
`ConnectionStrings__DefaultConnection` into the process environment. Environment variables
override User Secrets. The tool does not load `.env` or the API's settings automatically.

## Inputs

Keep real backups, classification decisions and reports **outside the repository**.
In Ingredient mode, only `ingredients.json`, `meta.json` and the explicitly selected
decision file are opened. The tool never enumerates or reads other exports, including
users and recipes.

Both export files are JSON arrays, matching the legacy Firestore document export:

```json
[
  {
    "id": "12345678-1234-4234-8234-123456789abc",
    "name": "Synthetic ingredient",
    "category": "Baking",
    "departments": { "woolworths-carlyle": "Aisle 7" },
    "productUrls": {
      "woolworths": [
        {
          "url": "https://example.test/product",
          "variantLabel": "Original",
          "note": "Synthetic example",
          "supportsBothPricingMethods": true
        }
      ]
    },
    "preferredVariant": "Original",
    "nutrition": {
      "per100g": { "calories": 100, "protein": 2, "sodium": 4 },
      "source": "synthetic",
      "lastUpdated": { "_seconds": 1769393852, "_nanoseconds": 474000000 }
    }
  }
]
```

The corresponding synthetic `meta.json` shape:

```json
[
  { "id": "categories", "list": ["Baking"] },
  { "id": "departments", "list": ["Aisle 7"] },
  {
    "id": "supermarkets",
    "supermarkets": [
      {
        "id": "woolworths-carlyle",
        "chain": "Woolworths",
        "name": "Synthetic store",
        "location": "Synthetic location"
      }
    ]
  }
]
```

Supermarket `id` values are validated against seeded store codes. Other supermarket
fields are reported as ignored; reference data is not imported.

Create a separate decision file, initially `{}`. Every `invalid: true` item needs
an explicit decision keyed by its **ingredient GUID**, not its name/document key:

```json
{
  "12345678-1234-4234-8234-123456789abc": {
    "name": "Synthetic water",
    "classification": "Ingredient"
  },
  "87654321-4321-4321-8321-cba987654321": {
    "name": "Optional:",
    "classification": "Heading"
  },
  "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee": {
    "name": "Synthetic serving note",
    "classification": "Note"
  }
}
```

Names are review labels only. `Ingredient` creates an excluded ingredient; `Heading`
and `Note` create no row. Suggestions are never accepted automatically. Retain entries
absent from newer backups for future recipe migration. A decision for a currently
non-invalid item, or Heading/Note with an existing Ingredient, blocks writes.
Back up the decision file together with exports through recipe and grocery migration.

## Preview and apply

`dotnet run` builds/runs the selected `--project`. `--configuration Release` uses a
separate output directory from a running Debug API. The standalone `--` passes the
following arguments to the tool rather than to .NET:

- `--backup`: explicit directory containing the two export files.
- `--owner`: existing destination user GUID.
- `--decisions`: explicit retained decision file.
- Ingredient mode is the default and continues to accept the existing arguments.
- Recipe mode is selected explicitly with `--mode recipes --recipes <file>` and requires
  `--report <file>` outside the repository. Repeat `--source-owner <legacy ID>` for each
  owner in a multi-owner export. For referenced images, pass both `--image-manifest <file>`
  and `--image-root <directory>`. It cannot be combined with `--backup` or `--write`.
- No `--write`: **dry-run**, including diagnostics, counts and complete update diffs.
- `--write`: fresh preview, individual update approvals, then one atomic commit.

Replace the synthetic paths and owner before executing:

```powershell
dotnet run --project tools\FirestoreMigration --configuration Release -- --backup "C:\MigrationBackups\latest" --owner "11111111-2222-4333-8444-555555555555" --decisions "C:\MigrationBackups\decisions.json"
```

Recipe preview command shape (Recipe mode is read-only):

```powershell
dotnet run --project tools\FirestoreMigration --configuration Release -- --mode recipes --recipes "C:\MigrationBackups\recipes.json" --owner "11111111-2222-4333-8444-555555555555" --decisions "C:\MigrationBackups\decisions.json" --report "C:\MigrationReports\recipe-preview-001.json"
```

The image manifest is a JSON array with one entry per Recipe image:

```json
[
  {
    "recipeId": "legacy-recipe-id",
    "imageReference": "https://example.test/recipe-image",
    "path": "images/recipe-image.png",
    "byteCount": 1234,
    "sha256": "64 hexadecimal characters"
  }
]
```

Only files selected by matched manifest entries are opened. Paths must be relative to
`--image-root` and remain inside it, including after resolving filesystem links. The
validator verifies the manifest byte count and SHA-256, identifies image type from decoded
content, and fully decodes PNG, JPEG or WebP. It rejects GIF, SVG and other formats,
files larger than 20 MiB, either dimension above 10,000 pixels, or images above 40
megapixels. Extensions and manifest MIME types are not trusted. Preview never uploads
images or writes image associations.

The JSON report includes one source disposition per Recipe, including whether it was
selected, the ordered source Steps/items, and each item's outcome (`ingredient`,
`heading`, `note`, or `blank-placeholder-omitted`). Field-level paths identify their
typed destination or retention, selection, and validation disposition. This makes
source counts and omissions reviewable without copying source values into console output.
The run summary separately counts planned Heading/Note occurrences and missing or
cross-owner Ingredient references; repeated uses count as separate occurrences.

Review the printed server, database and destination owner. Fix blocking diagnostics
and classify every unresolved invalid item. Source names are preserved unchanged;
normalised names are used only for matching and uniqueness. Business-rule nutrition
violations warn but retain representable source values. Values not exactly representable
as `numeric(9,3)`, and timestamps exceeding PostgreSQL microsecond precision, block
rather than being silently rounded.

For Ingredient mode, append `--write` to the same command. New ingredients need no individual
approval. Each changed ingredient prints all differences and requires **`yes`**.
Any other answer (including EOF) declines that entire ingredient and its children.
There is no unattended accept-all option. Ctrl+C before commit cancels the run.

Accepted updates replace source-controlled fields, including removals and cleared values.
IDs, owner, creation and archive state are preserved. Source nutrition time is preserved.
Unchanged mappings/links retain their keys. Master category/department lists are additive;
values used only by declined ingredients are not created unless also in a master list.
Target ingredients absent from a backup remain untouched.

Inputs are SHA-256 fingerprinted at preview. Before writes, the tool locks migration
tables, checks PostgreSQL `xmin` versions, and rehashes/holds all three input files
read-only through commit. Database locks are held **only after prompts**, with a
10-second acquisition timeout. The version read-set is intentionally conservative:
any change to ingredient, lookup, store, chain, mapping or link tables (even another
owner's change) invalidates the plan. It detects child-only changes and concurrent
insertions as well as parent edits. No other owner's ingredient content is loaded
or printed. A conflict requires a fresh run/review; no partial migration is committed.

Exit codes: `0` successful preview/write, `1` blocking diagnostics or operational
failure, `2` cancellation. The final summary distinguishes inserted, updated,
unchanged, declined, headings, notes, warnings and errors. Preview additions are
**proposed**, not counted as inserted. Declined updates remain visible next run.
Database failures show SQLSTATE without printing connection strings.

There is no previous-import snapshot or three-way merge. Approval explicitly selects
incoming values over application edits. A write constraint failure, including a
rename dependency that cannot satisfy immediate unique constraints, rolls back the
entire transaction; resolve it explicitly rather than merging distinct IDs.

## Rehearsal, cutover and cleanup

1. Rehearse against the persistent **local** database with the registered owner.
2. Preview, resolve errors/classifications, then explicitly write and review each update.
3. Inspect counts, relationships, link ordering, preferred links, exclusion flags and
   representative nutrition values through the Ingredient Maintenance list/detail API.
4. Run the same backup again: accepted records must be unchanged, while declines
   remain proposed. Restart PostgreSQL **without deleting its named volume** and
   verify the same records remain accessible.
5. While the legacy app is active, repeat with newer backups as needed.
6. Before production cutover, separately provision/rehearse hosting, restricted
   connectivity, secret injection and backup/restore. Stop legacy edits, take a final
   backup, migrate the production schema, verify the destination identity/owner,
   import, resolve all declines/errors, and verify before switching usage.
   Recipe and grocery verification belongs to their later migration PRDs.

No production or personal-data import is performed by the automated tests.
Unit and Reqnroll tests use synthetic inputs; component scenarios use isolated
Testcontainers PostgreSQL databases and are included in the existing CI test commands.

After Recipe and Grocery List migration and final verification, remove this directory,
the solution entry, the tool references and `Migration` test directories from both API
test projects, the API's `InternalsVisibleTo` entry, and the tool's User Secrets.
Retain exports, decisions and final reports according to your backup policy.
There are no application routes, registrations, feature flags or schema changes to remove.
