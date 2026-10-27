# PRD 04: Recipe Foundation, Migration & Read-Only Browsing

## Status & Traceability

- Status: Proposed; grounded in the current implementation and a supplied legacy export.
- Epic: [#53 - Recipe Foundation, Migration & Read-Only Browsing (PRD04)](https://github.com/lhjenner/recipe-app-monorepo/issues/53), linked to the Recipe App project.
- Implementation sub-issues: [#54 (S1)](https://github.com/lhjenner/recipe-app-monorepo/issues/54), [#55 (S7)](https://github.com/lhjenner/recipe-app-monorepo/issues/55), [#56 (S2)](https://github.com/lhjenner/recipe-app-monorepo/issues/56), [#57 (S3)](https://github.com/lhjenner/recipe-app-monorepo/issues/57), [#58 (S4)](https://github.com/lhjenner/recipe-app-monorepo/issues/58), [#59 (S5)](https://github.com/lhjenner/recipe-app-monorepo/issues/59), and [#60 (S6)](https://github.com/lhjenner/recipe-app-monorepo/issues/60). See section 14 for scope and sequencing.
- PRD pull request: [#52](https://github.com/lhjenner/recipe-app-monorepo/pull/52). Implementation pull requests: Not yet created.
- Depends on: [PRD01 - Authentication & Login](./01-authentication-and-login.md), [PRD02 - App Shell & Navigation](./02-app-shell-and-navigation.md), and [PRD03 - Ingredient Maintenance](./03-ingredient-maintenance.md).
- Reuses the Ingredient foundation from [epic #39](https://github.com/lhjenner/recipe-app-monorepo/issues/39), particularly the migration tool introduced by [issue #42](https://github.com/lhjenner/recipe-app-monorepo/issues/42).
- Each implementation issue and PR must link this PRD and identify its acceptance criteria. No issue number in this document implies that PRD04 work has already been created or completed.

## 1. Overview & Problem Statement

The rewritten application has a populated, user-owned Ingredient catalogue but no Recipe domain. Recipes are the next foundation for Grocery Lists, Meal Planning and Food Diary. Migrating recipes must retain the existing Ingredient GUID references and the legacy step-based structure without encoding headings and notes as fake Ingredients.

PRD04 establishes durable Recipe persistence, extends the standalone import tool, and replaces the Recipe List stub with lean, read-only browsing and detail screens. It does not introduce recipe authoring or downstream integrations.

**Storage clarification:** Firestore is the legacy source, exported as JSON. PostgreSQL through EF Core remains the destination. Firestore recommendations in this PRD describe source interpretation and mapping, not a second application database or a Firestore redesign.

Existing recipe images also move out of legacy Firebase Storage into private object storage. PostgreSQL stores their associations and verification metadata, not image bytes. Neon is the recommended provider for PostgreSQL and object storage, subject to the compatibility and free-plan checks below; application hosting is a separate decision.

## 2. Scope, Goals & Non-Goals

### In scope

- User-owned Recipes with ordered Steps and ordered, typed Step Items.
- GUID Recipe identities with original Recipe IDs retained as `LegacyId`; preservation of Ingredient references, metadata, source content and ordering.
- Import of active and archived recipes, preserving their archive state.
- Explicit conversion of legacy structural ingredient entries to Heading or Note items using retained PRD03 decisions.
- Lossless source retention, validation, repeat imports and durable migration reports.
- Authenticated active Recipe List, recipe-name/ingredient-name search, and read-only Recipe View.
- Migration and verification of existing image files, private storage, and display of one image on Recipe View.
- Responsive layouts, accessible interaction, loading, empty, not-found and error states.

### Goals

1. Make every imported recipe traceable to its source, with no silent loss.
2. Keep real ingredient references usable by future domains.
3. Preserve step-level boundaries so future editing does not require replacing the aggregate.
4. Make it easy to find and read an active recipe on mobile or desktop.
5. Support repeat migration while the legacy application remains in use.

### Non-goals

- Recipe creation, editing, duplication, deletion, archive or restore actions.
- Archived recipe browsing, including archived detail access through the new Recipe View.
- Tag, source, category, metadata or advanced filtering; user-selectable sorting.
- Favorites, new recipe categories or taxonomy-management screens.
- Grocery List, Meal Planner or Food Diary integration.
- Scaling, multipliers, quantity arithmetic, unit conversion or nutrition calculation.
- User-facing image upload, replacement or deletion; galleries, cropping, transformations and Recipe List thumbnails.
- In-app import/upload endpoints, production provisioning or changes to authentication.
- A new Firestore target model, live Firestore access, synchronization or dual writes.

The existing Add Recipe and Archived Recipes navigation destinations remain stubs. Preserve the app shell and menu ordering; do not make those destinations functional as an incidental part of this PRD.

### Hosting, storage and cost constraints

- Target recurring cost is $0 using genuine free plans, not time-limited trials or promotional credits. Self-hosting is excluded.
- Recommend Neon Free PostgreSQL for application data and Neon private Object Storage for images. Database storage and object storage have separate quotas; do not count images against the database allowance or store them as database blobs.
- Provider selection is conditional on verifying available regions, private-object access, .NET/S3 compatibility, quota-exhaustion behavior, transfer/operation limits and recovery/export capability. Prefer hard limits that stop service rather than automatically charge. Do not enable a paid plan or billing-based fallback without an explicit decision.
- Record checked plan terms and date in the implementation issue; do not treat today's advertised allowances as permanent guarantees. Check compute, bandwidth and requests as well as storage. Accept database cold starts on a free plan; avoid artificial keep-alive traffic.
- Neon is not the selected .NET application host. Its documented Node.js Functions runtime does not run this ASP.NET Core API. Choose a genuine-free .NET/container host separately; no backend rewrite is implied.
- Private cloud object storage is the authoritative recipe image collection during local development as well as after deployment. Select and verify the provider during PRD04; the locally running API accesses it using server-side credentials before a hosted site exists. No persistent local collection, offline image mirror or manual synchronization is required.
- Real recipe images must not be committed to Git, bundled into the frontend public assets, stored in a container's disposable filesystem or exposed through a public bucket. Temporary migration downloads are transfer inputs, not an ongoing application dependency, and may be removed after verified import and backup.
- Automated tests use small synthetic image fixtures committed to the repository and isolated temporary/fake storage, not real recipe photos or a live cloud account. Actual-data rehearsal uses the selected private cloud storage.
- Implement a small storage boundary for private object reads and migration writes, with isolated test storage and the selected provider adapter. Keep object keys independent of public URLs and avoid a generic media platform. Local and hosted browsers use the same authenticated application image endpoint contract; only API origin, credentials and environment configuration differ.
- Use an isolated development bucket initially and a separate production bucket at go-live. Transfer verified objects and matching database associations explicitly, retaining object keys where practical. These are independent environments, not two collections the user must keep manually synchronized.
- Backups are recoverable snapshots/version history, not working image copies. Verify and document an automated cloud backup/versioning and database-association recovery approach within the free-plan constraints; do not assume the provider includes one or require the user to maintain a local mirror. If no suitable $0 recovery option exists, surface that gap for an explicit decision before cutover.
- Production provisioning remains separate, but the selected storage adapter, private delivery and migration must be exercised before declaring image migration complete.
- Reference checks: [Neon pricing](https://neon.com/pricing), [Object Storage](https://neon.com/docs/storage/overview), and [Functions runtime](https://neon.com/docs/compute/functions/reference/runtime-limits). These are recommendations, not evidence that an account or deployment has been provisioned.

## 3. Verified Baseline & Source Findings

### Current implementation

| Evidence | Verified behavior | Implication |
|---|---|---|
| [AppDbContext](../../apps/api/src/RecipeApp.Api/Infrastructure/Persistence/AppDbContext.cs) | One EF Core context; no Recipe entities currently registered | Extend the existing context and use EF migrations |
| [Ingredient](../../apps/api/src/RecipeApp.Api/Infrastructure/Persistence/Ingredient.cs) and its [configuration](../../apps/api/src/RecipeApp.Api/Infrastructure/Persistence/Configurations/IngredientConfiguration.cs) | GUID primary key, owner, archive timestamp, restrictive relationships and PostgreSQL `xmin` version | Keep Ingredient IDs; enforce same-owner references; archived Ingredients remain valid references |
| [IngredientReadService](../../apps/api/src/RecipeApp.Api/Ingredients/IngredientReadService.cs) | Owner-scoped reads, escaped PostgreSQL `ILIKE`, deterministic paging | Reuse these patterns, not necessarily the Ingredient DTOs |
| [Migration tool guide](../../tools/FirestoreMigration/README.md) | Offline JSON inputs, default dry-run, existing destination owner, per-record update approval, atomic writes and source/target conflict checks | Extend this tool rather than building another importer |
| [SourceParser](../../tools/FirestoreMigration/SourceParser.cs) | Retained GUID-keyed Ingredient/Heading/Note decisions; Heading and Note create no Ingredient row | Resolve classification explicitly; never infer type from a missing row |
| [MigrationPlan](../../tools/FirestoreMigration/MigrationPlan.cs) and [MigrationRunner](../../tools/FirestoreMigration/MigrationRunner.cs) | Console reporting; unknown fields counted as ignored; no durable recipe report or source archive | Recipe losslessness and persistent reporting require new behavior |
| [App routes](../../apps/web/src/App.tsx) and [navigation](../../apps/web/src/navigation.ts) | `/recipes`, `/recipes/new` and `/recipes/archived` are existing destinations | Replace only the list stub and add detail routing without capturing reserved destinations |
| [Ingredient API client](../../apps/web/src/services/ingredientsApi.ts), [list hook](../../apps/web/src/hooks/useIngredientList.ts), and [title-case helper](../../apps/web/src/titleCase.ts) | Zod boundaries, shared API client, debounced/cancellable fetching and shared display casing | Follow these conventions for Recipes |

### Supplied recipe export

The supplied local export was inspected read-only. It is not copied into the repository and is not an automated-test fixture. These counts describe this snapshot, not permanent product limits or a successful import:

| Observation | Result |
|---|---|
| Recipes | 110; all IDs are unique, 20-character alphanumeric strings, not GUIDs |
| Structure | 462 Steps and 1,399 source ingredient-line occurrences |
| Empty structure | 16 Steps have empty ingredient arrays; 10 have blank instructions |
| Blank placeholders | 37 ingredient-line occurrences have blank IDs and every other present field blank |
| Other references | The other 1,362 ingredient-line occurrences have GUID-format IDs; database resolution and classification are not yet verified |
| Archive state | 1 explicitly archived, 38 explicitly active, 71 omit `archived` |
| Legacy ownership | 44 include `userId`; 66 omit it |
| Dates | 79 include `dateAdded`, using two timestamp encodings; 31 omit it |
| Images | 78 include `imageUrl`; image availability was not tested |
| Image collection size | Approximately 45 MB in Firebase Storage, reported by the owner; byte totals, referenced coverage and file types still require inventory |
| Line fields | `id`, `name`, `quantity`, `unit`; optional `notes` and legacy `department` |
| Free text | Quantities include fractions, ranges, embedded units and a nonnumeric label; servings and times also contain descriptive text |
| Structural labels | Examples include Optional, Setup, Full Recipe, Half Recipe and serving/topping labels |

No live database, retained classification file or matching ingredient export was supplied for this inspection. This PRD does **not** claim all Ingredient references resolve, that all structural labels are classified, or that import is ready to write.

## 4. User Stories

- As a user, I want my existing recipes migrated with their legacy identifiers retained so that old references can resolve to stable new Recipe identities.
- As a user, I want each recipe's steps and ingredient lines kept in order so that I can follow the original method.
- As a user, I want headings and notes displayed as structured content so that serving suggestions are not mistaken for ingredients.
- As a user, I want to search by recipe name or an ingredient's name so that I can find meals using that ingredient.
- As a user, I want to read recipe metadata and instructions on any device so that I can use the recipe while cooking.
- As a user, I want my existing recipe photos migrated and displayed privately so that I can recognize a dish without depending on the legacy storage account.
- As a migration operator, I want explicit validation and reconciliation reports so that I can resolve problems before switching applications.
- As a future feature developer, I want stable Recipe, Step and Ingredient references so that authoring and downstream features can build on this foundation.

## 5. Domain Model Recommendations & Entity Definitions

### Aggregate and identity

Use `Recipe -> RecipeStep -> RecipeStepItem`, with Ingredient items referencing the existing Ingredient entity. Tags belong to the Recipe; source is recipe metadata, not a new managed taxonomy.

- Recipe `Id` is a GUID v7 stored as PostgreSQL `uuid`. Assign it on the first successful import or, in a future authoring PRD, when a Recipe is created in this app.
- Imported Recipes retain the exported `id` exactly in `LegacyId: string?`, stored as PostgreSQL `text` with deterministic, case-sensitive equality. Do not lowercase, trim or constrain it to exactly 20 characters merely because this sample uses that format.
- Recipes created in this app have `LegacyId = null`. Creation remains out of scope; the persistence model must support this case without requiring a fabricated legacy identifier.
- Enforce a unique filtered index on `(OwnerUserId, LegacyId)` where `LegacyId IS NOT NULL`, and reject non-null blank values. Multiple Recipes per owner may have null `LegacyId`; the same legacy value may exist for different owners.
- Treat `Id` and `LegacyId` as immutable once imported. Repeat imports resolve `(OwnerUserId, LegacyId)` and reuse the stored GUID. `LegacyId` provides the mapping directly; no separate mapping table is required.
- All new Recipe foreign keys, public API identifiers and recipe URLs use the GUID `Id`. Future imports of legacy Recipe references resolve them through the destination owner and `LegacyId`, then persist the GUID FK. Ingredient GUIDs remain unchanged.
- New internal Step, Step Item and tag-row IDs can use GUIDs. Imported Steps/items have no source IDs.
- Persist an explicit zero-based `Position` for Steps, items and tags. Enforce uniqueness within the parent. Do not rely on insertion order, dictionary order or primary-key order.
- Keep duplicate ingredient occurrences and tags, including repeated use of the same Ingredient in one Step. Do not aggregate quantities or deduplicate source arrays.
- Do not impose Ingredient-style recipe-name uniqueness: a recipe's identity is its ID, not its name.

### Recommended entities

| Entity | Fields / relationships | Rules |
|---|---|---|
| Recipe | `Id: Guid`, `LegacyId: string?`, `OwnerUserId: Guid`, `Name: string`, `NormalizedName: string`, `SourceText`, `SourceUrl`, `ServingsText`, `PrepTimeText`, `CookTimeText`, `DateAddedAtUtc?`, `IsArchived: bool`, application `CreatedAtUtc` / `UpdatedAtUtc`, `Version` | GUID v7 primary key; nullable legacy identity unique per owner when present; owner FK to existing User; source text preserved; original date distinct from import/audit time; `xmin` supports import conflict detection |
| RecipeImage | `RecipeId: Guid` primary key/FK, `ObjectKey: string`, `ContentType: string`, `ByteSize: long`, `Sha256: string`, `Width: int`, `Height: int` | Optional one-to-one relationship; privately stored original bytes, positive size/dimensions, immutable object revision; no Firebase URL, credentials or signed URL in the application image reference |
| RecipeStep | `Id: Guid`, `RecipeId: Guid`, `Position: int`, `Instructions: string` | One Recipe; ordered; instructions may be empty; retain line breaks; ingredient-free Steps are valid |
| RecipeStepItem | `Id: Guid`, `RecipeStepId`, `Position: int`, `Kind: Ingredient / Heading / Note`, `IngredientId: Guid?`, `Text: string`, `QuantityText`, `UnitText`, `NotesText` | Shared ordered list; Ingredient requires a real FK; Heading/Note require null Ingredient FK; never create fake Ingredient rows |
| RecipeTag | `Id: Guid`, `RecipeId: Guid`, `Position: int`, `Text: string` | Ordered source values, not a global tag catalogue; no tag-management behavior |
| RecipeImportProvenance | GUID Recipe FK, source document JSON, document SHA-256, source-file SHA-256, mapping-version identifier, decision fingerprint, last applied run identifier/time | Non-public persistence concern, separate from the authoring domain; contains enough source detail to verify every mapping and omission |

`Text` means the imported ingredient-name snapshot on Ingredient items and the original label on Heading/Note items. Ingredient display uses the current linked Ingredient name; the snapshot remains available for migration comparison. Quantity, unit and notes remain separate even on structural items if the source contains them.

Use nullability to represent absent optional fields; do not invent values. Retain the distinction between absent, null and empty in provenance even where the read model presents all three as "not supplied".

### Relationship and persistence rules

- Extend `AppDbContext`, with separate `IEntityTypeConfiguration<T>` classes and explicit EF migrations. Do not create a second database context or use production raw SQL scripts for schema/data changes.
- Restrict Ingredient hard deletion while referenced. Archiving an Ingredient must not break Recipe display or ingredient-name search.
- Guarantee that each Ingredient item references an Ingredient owned by the Recipe owner, at both migration/application validation and database-integrity boundaries. A composite owner-aware FK is recommended; carry the owner through child relationships as necessary. Do not rely solely on UI filtering or a single-column Ingredient FK.
- Enforce `Kind`/FK consistency, nonnegative positions and parent/position uniqueness. Restrict Recipe ownership reassignment through import.
- Permit empty Steps and empty instructions. Retain an entirely empty Step if present in a future export; do not collapse it.
- Keep quantities, units, servings and times as text in PRD04. Future numeric interpretation must be additive, nullable and explicit; it must not overwrite the source text.
- Apply title case at display time with the shared helper for recipe and Ingredient names. Do not rewrite stored names, instructions, URLs or labels to achieve display formatting.
- Keep raw source JSON out of public DTOs and ordinary logs. Its retention is deliberate even after the temporary migration executable is removed.

### Future step-level editing

Persist child IDs so future APIs can address individual Steps/items. On repeat imports during this read-only phase, match children by their source position and reuse existing IDs at matching positions; unchanged runs must preserve all child IDs. Report inserted, removed or reordered content as part of the complete recipe update.

Positions are not permanent semantic identities. Before recipe authoring or downstream Step references are enabled, a later PRD must define reconciliation or stop legacy re-import for authored recipes. Do not claim positional matching is a safe merge strategy after local recipe editing begins.

## 6. Firestore Source Model & Mapping

Do not modify the legacy Firestore collections to make this import work. Consume the supplied JSON-array export through an explicit recipe import mode; no Firestore database credentials or SDK are required. Obtain image files separately through an operator-controlled Firebase Storage export/download, then import the explicit local image manifest and files. Any credentials needed for that export remain outside the application and repository.

| Legacy JSON path | Destination / interpretation |
|---|---|
| `id` | Exact `Recipe.LegacyId`; resolve by destination owner and this value, assigning a GUID v7 `Recipe.Id` only for a new imported Recipe |
| `title` | `Recipe.Name`; derived normalized name for deterministic ordering only |
| `source` / `link` | `SourceText` / `SourceUrl`, retained independently |
| `servings`, `prepTime`, `cookTime` | Text fields, without numeric coercion |
| `tags[]` | Ordered RecipeTag rows, with original text retained |
| `steps[]` | Ordered RecipeSteps |
| `steps[i].instructions` | Step instructions with original whitespace and line breaks |
| `steps[i].ingredients[j]` | A typed item, or a reported blank placeholder retained in provenance |
| Line `id` | Existing Ingredient GUID, or lookup key into retained PRD03 classification decisions |
| Line `name` | Original item text/name snapshot, never an identity-resolution fallback |
| Line `quantity`, `unit`, `notes` | Separate text values; retain absent versus empty in source JSON |
| Line `department` | Preserve in provenance and report as retained legacy-only metadata; do not override current Ingredient store mappings |
| `archived` | `IsArchived`; absent means active, with an explicit defaulting diagnostic |
| `dateAdded` | Original nullable date, distinct from application audit fields |
| `imageUrl` | Original URL retained privately in provenance; explicit image manifest resolves it to verified local bytes and then a RecipeImage association in private storage |
| `userId` | Legacy provenance only; never used as the new application's owner FK |
| Any unrecognized property | Preserve in source JSON and emit a field-path warning; never silently ignore |

Recognize both observed timestamp shapes: `_seconds` / `_nanoseconds` and `seconds` / `nanoseconds` with `type: "firestore/timestamp/1.0"`. Validate seconds, nanosecond range and destination precision. Block unsupported or lossy typed conversion instead of rounding; retain the source in all cases. Missing `dateAdded` stays null.

The source `id` is the exported legacy recipe identifier, not the new primary key. Unlike Ingredient IDs, it is not a GUID. Retain it unchanged and use the persisted `LegacyId`-to-`Id` relationship for migration compatibility. There is no evidence here requiring a new Firestore document hierarchy, subcollection conversion or new source ID scheme.

## 7. Recipe Import Strategy

### Extend, do not replace, PRD03 tooling

Add an explicit recipe mode to [FirestoreMigration](../../tools/FirestoreMigration/README.md). Preserve the current Ingredient invocation and behavior unchanged. Recipe mode must not import Ingredients, users or unrelated exports as a side effect.

Reuse JSON duplicate-key checks, safe configuration, owner validation, schema checks, dry-run planning, complete diffs, individual approvals, cancellation, input fingerprints and post-approval conflict detection. Extend source selection, planning, writing, target snapshots and reporting only where Recipes require it.

### Inputs and ownership

- Explicit recipe JSON path, existing destination owner GUID, retained PRD03 decision file, and report destination outside the repository.
- Explicit image manifest and backup directory when images are referenced. The manifest maps source Recipe IDs and original image references to relative file paths, byte counts and SHA-256 checksums. Include it and the referenced bytes in input fingerprint/conflict checks; never enumerate unrelated files.
- A matching legacy ingredient export is required when the original text of a classified structural GUID is not available on the recipe line. A decision-file `name` is only a review label, not authoritative replacement content.
- Retain original exports unchanged, with file SHA-256 checksums. Do not enumerate unrelated backup files.
- Print and confirm the destination owner and source selection. Display counts of present/missing legacy owner fields; if multiple source owners occur, block until the operator explicitly selects an allowed source set.
- This single-owner export can be assigned to the selected destination owner through explicit operator confirmation. Missing legacy `userId` is reported, not silently mapped to a guessed account.
- Refuse nonexistent owners, ownership conflicts, pending migrations or model/schema mismatch. The tool must not register accounts or migrate the database automatically.

### Classification and blank content

For each source line in original order:

1. If all present fields are empty strings, treat it as a blank placeholder. Retain the complete object and original position in provenance, omit a visible item, and report the omission. Any nonempty or unknown-valued field prevents this rule from applying.
2. For a GUID classified Heading or Note, create that item type at its original position, without an Ingredient FK. Use original line text, with an authoritative legacy ingredient record as a documented fallback when necessary. Do not use punctuation or a missing database row to infer classification.
3. For an Ingredient classification or an ordinary GUID, require a same-owner Ingredient row. Preserve the GUID and all line text. An archived Ingredient or `ExcludeFromGroceryList = true` Ingredient is valid.
4. An unresolved classification, a Heading/Note decision conflicting with an existing Ingredient row, a missing Ingredient, a nonblank invalid ID or unavailable structural text blocks the run. Do not resolve by fuzzy/name matching, mint a new Ingredient, drop the line or convert it to Note automatically.

Heading/Note conversion preserves quantity, unit and notes too; the view must not hide nonempty values attached to a converted label. Sparse item positions are valid after omitting blank placeholders.

### Apply and repeat behavior

- Dry-run is the default and performs no database writes. Report proposed inserts/updates/conversions separately from committed results.
- Match source Recipes by `(destination OwnerUserId, exact source id as LegacyId)`, never by name or by interpreting the source ID as a GUID. A match retains its existing `Id`; an unmatched record receives a GUID v7 on insertion. Preview reports must not present an uncommitted proposed GUID as an established mapping.
- Source imports must not attach themselves to a Recipe with null `LegacyId`, even when names match. Concurrent attempts to insert the same owner/legacy identity must be detected before commit or rejected by the unique index with a complete rollback.
- An explicit write run makes a fresh preview. New Recipes require no per-recipe approval; changed Recipes require approval of the entire Recipe, including metadata, children and provenance changes.
- Unchanged Recipes are skipped. Declined updates leave the complete target Recipe unchanged and remain proposed on a later run.
- Never delete destination Recipes absent from the source export.
- Preserve Recipe GUID `Id`, `LegacyId`, owner and existing application creation time on updates. Apply the source archive state as an explicitly displayed, approved field change.
- Source changes to arrays, unknown fields or other provenance content must not be hidden by a typed-field-only comparison. JSON object-property order alone does not constitute a content change; array order does.
- Commit all accepted database changes, including image associations, in one transaction. Any blocking validation or pre-commit write failure means zero accepted Recipes are committed; this PRD does not introduce partial-success database batches. Object writes cannot share that transaction; the image staging and recovery rules below apply.
- After approvals, recheck all selected input fingerprints and the relevant target read-set, including referenced Ingredients, classifications, Recipe children and concurrent insertions. Hold verified inputs through commit and use bounded locking consistent with PRD03. Do not hold database locks while prompting.
- Cancellation before commit rolls back all writes. Operational failures must be explicit, not disguised as successful zero-count imports.
- Repeat the same accepted input: resolve the same legacy-to-GUID mappings, zero inserted/updated Recipes, unchanged child IDs, and no duplicate tags, Steps or items.

### Existing-image migration and recovery

1. Inventory source references and export original image bytes before retiring Firebase. Resolve filenames/references through exact `LegacyId`, not a guessed new GUID. Do not assume every URL has a file or every bucket file belongs to a Recipe. Keep unreferenced files in the backup and report them; do not delete them automatically.
2. The importer reads only manifest-selected local files. Reject path traversal/out-of-root paths, unsupported image formats and invalid/mismatched content. Validate file signatures, successful decoding, dimensions, byte limits and checksum, rather than trusting extension or MIME labels. Document finite byte/dimension limits and supported inert raster formats in the tool guide; unsupported content blocks rather than being silently transcoded or lost.
3. Default dry-run validates local bytes and reports proposed image work without uploading or modifying target objects. No arbitrary URL fetching is performed by either the application API or recipe importer. Downloading source images is a separate, explicit export step.
4. After whole-Recipe approvals, stage only accepted image revisions under opaque run-specific object keys in private target storage, with create-only/immutable semantics. Verify stored size and SHA-256 before the short database transaction. Never overwrite an object referenced by an existing Recipe.
5. Commit RecipeImage metadata with the accepted Recipe. Source images are imported for archived Recipes too, although archived image delivery remains unavailable. Empty/absent source images mean no image; a nonempty reference with missing/unverifiable bytes blocks the write run and is not equivalent to "no image".
6. Preserve an existing image until a complete Recipe update, including any changed or removed image association, is approved and committed. Compare checksums even if the source URL is unchanged. An unchanged, verified manifest/image must retain the association and avoid another upload.
7. Journal staged keys and checksums outside the repository so retries can validate and reuse this run's objects. If validation, concurrency, cancellation or commit fails, database changes roll back; staged objects may remain. Report them as unreferenced, not migrated. Cleanup is explicit and limited to journaled, confirmed-unreferenced keys, never a bucket-wide delete.
8. Retain superseded objects until backup and migration verification are complete. Before any cleanup, confirm no committed association references the object. If a commit outcome is uncertain, reconcile database/run state first; do not assume rollback and delete possibly referenced files.

Local export files are point-in-time migration inputs, not a second authoritative collection. After verified import, report reconciliation and recoverable cloud backup, they may be removed through explicit targeted cleanup. Repeat imports use a fresh export when needed; never require ongoing local edits to track image changes. Retain manifests, checksums and reports in restricted durable storage for traceability.

An image failure during import is a blocking migration diagnostic by default; it does not prevent already imported recipe text from being read. Any decision to defer a referenced image requires a recorded scope exception before cutover, not an automatic fallback to the Firebase URL.

### Losslessness contract

Every source field must be either represented in the typed model or retained in per-recipe source JSON with a reported disposition. Keep original backup bytes separately; PostgreSQL JSON storage is not a byte-for-byte archive and does not preserve object-key order or lexical number formatting.

Unknown metadata and legacy-only fields are retained even if not yet rendered. This is not permission to hide a required user-facing field: all known recipe metadata in scope must be mapped and displayed. Newly discovered meaningful fields require a documented mapping decision before cutover.

Report and source files can contain personal data or access-bearing URLs. Keep them outside version control with restricted access; redact credentials, URL query tokens and legacy user identifiers from ordinary console output. Never commit the supplied export or use it directly in automated tests.

## 8. Migration Validation Rules

| Area | Blocking condition | Valid/warning condition |
|---|---|---|
| JSON | Malformed input, duplicate object keys, wrong root/field types | Unknown fields retained with field-path warnings |
| Recipe identity | Blank/duplicate source ID within the selected owner, duplicate owner/LegacyId mapping, empty/colliding target GUID or attempted identity reassignment | Preserve exact source ID as case-sensitive LegacyId; reuse an existing mapping or assign a new GUID v7; duplicate names and multiple null LegacyIds are allowed |
| Recipe content | Missing/blank title; missing or non-array Steps; non-string instructions; malformed tags/items | Empty arrays, instructions and optional metadata remain valid |
| Ingredient identity | Nonblank invalid GUID, missing or different-owner Ingredient | Archived and grocery-excluded Ingredients remain usable |
| Classification | Missing required decision, invalid classification, contradictory Ingredient row, lost structural text | Explicit Heading/Note decisions create typed non-Ingredient items |
| Line content | Unexpected field types or nonempty unresolvable lines | Fractions, ranges, embedded units and unusual text retained with anomaly warnings |
| Placeholders | Blank ID with any meaningful content or unknown nonempty field | Entirely blank object retained and omission counted |
| Ordering | Duplicate generated parent/position keys or changed order during mapping | Empty Steps and repeated Ingredient occurrences preserved |
| Dates | Invalid timestamp shape/range or precision loss in typed conversion | Missing original date stays null; supported shapes normalize to UTC |
| Archive/owner | Nonboolean archive value or unapproved source-owner selection | Missing archive defaults to active with diagnostic; missing legacy owner reported |
| URLs | Non-string source/image value | Blank allowed; malformed/unsafe URLs retained with warning but never made clickable |
| Images | Nonempty image reference missing from manifest, missing/changed file, path escape, checksum mismatch, unsupported/invalid image or storage verification failure | Absent/empty reference needs no image; verified original bytes are copied privately, without transformation |
| Target/input state | Schema drift, changed source/decisions or changed target after preview | Fresh preview required after any conflict |
| Completeness | Any source field or line lacks a typed mapping or retained disposition | Unknown data retained and listed for review |

Before cutover, reconcile source/target counts, ordered content and representative rendered Recipes. There must be no unresolved missing Ingredients, classifications, blocking failures or declined updates required for cutover. Unknown-field and anomaly warnings need recorded review, not merely a zero-error run.

## 9. Migration Reporting Requirements

Produce human-readable console output and a machine-readable JSON report at an explicit local destination. The report is required for preview and write attempts; it is not a new public API or in-app dashboard.

### Run-level information

- Run ID, tool/mapping version, UTC start/end, mode, destination owner, redacted server/database identity.
- Source-file and decision fingerprints; source selection/ownership confirmation; report schema version.
- Terminal status: preview-valid, validation-blocked, committed, cancelled, rolled-back, or operational-failure.
- Counts for source Recipes, proposed new/changed/unchanged, approved/declined, committed inserted/updated/unchanged, failed validation, and valid-but-not-applied Recipes.
- Headings Converted, Notes Converted, Missing Ingredients, blank placeholders omitted, unknown fields retained, warnings and errors.
- Conversion counts distinguish planned and committed occurrences. Count occurrences separately from distinct legacy IDs.
- Archive defaults, missing original dates/owners, retained image URLs, legacy departments and anomalous text summarized explicitly.
- Image references, no-image Recipes, proposed/reused/uploaded/associated images, missing/invalid files, verification/download-export failures, bytes/checksums, unreferenced source files and staged/superseded objects awaiting cleanup. A successful upload is not a committed Recipe association; report both counts separately.

### Per-recipe diagnostics

- Source `LegacyId` and mapped Recipe GUID `Id` when available, outcome, ordered field diffs, error/warning code, JSON field path, source Step/item positions and affected Ingredient GUID where appropriate. New-record previews may have no target ID; committed reports must include both IDs for every applied Recipe.
- Preserve JSON-path context such as `steps[2].ingredients[1]`; human labels may additionally use one-based Step numbers.
- Distinguish invalid source, dependency missing, declined, unchanged, not applied due to another blocker, and rolled back. Do not label every uncommitted Recipe "failed".
- Counts must reconcile: every source Recipe has exactly one terminal outcome. Dry-runs report zero committed imports; rolled-back runs report zero committed inserts/updates.
- Record ordered source Step/item totals and dispositions, enabling verification that each source item became an Ingredient, Heading, Note or retained blank placeholder.
- Record the image outcome, original reference fingerprint, checksum, object key and association/cleanup status when applicable. Full source access URLs and tokens stay in restricted provenance/backup, not console logs; distinguish image failure from no-image and database rollback from staged-object persistence.

Prepare a writable report destination before write mode starts. Filesystem reports and database transactions are not one atomic resource: retain a run/commit identifier with applied provenance and record the commit outcome explicitly. If final report persistence fails after a database commit, exit nonzero, state that the data **was committed**, and provide recovery instructions/run identification; do not claim rollback or encourage a blind retry.

Reuse exit codes `0` for successful preview/write, `1` for blocking/operational failure, and `2` for cancellation. Persistent reports must not overwrite earlier runs unintentionally.

## 10. Read API & Search Requirements

Follow the existing Controller -> application read service -> EF persistence pattern. Return dedicated DTOs, not entities or raw provenance. Use async I/O and pass CancellationTokens throughout.

| Endpoint | Contract |
|---|---|
| `GET /api/recipes?search=&page=1&pageSize=25` | Active, current-owner Recipes only; `items`, `totalCount`, `page`, `pageSize`; summaries need only GUID ID and name |
| `GET /api/recipes/{id}` | GUID Recipe ID; current-owner, active Recipe metadata, ordered tags, ordered Steps and discriminated typed items; includes current Ingredient names and preserved line text |
| `GET /api/recipes/{id}/image` | Owner-authorized active Recipe image bytes from private storage; validated content type, size and checksum-derived ETag; never redirects to Firebase or exposes storage credentials |

Serialize Recipe IDs as UUID strings and validate them as UUIDs at frontend boundaries. `LegacyId` is for migration compatibility, not a second public lookup key; no legacy-ID endpoint or redirect is required in PRD04. A malformed GUID detail route returns `404`, consistent with a GUID-constrained API route.

Detail DTOs expose nullable image metadata (application-relative delivery URL, dimensions and revision/checksum), not storage keys or original URLs. Missing image association, missing Recipe, different owner and archived Recipe return `404` from the image endpoint; anonymous requests return `401`. An unexpectedly missing stored object is a logged server failure, and storage outage/quota failure returns explicit `503` Problem Details, not a successful placeholder image.

Authorize every image request, including conditional requests, before returning `304`. Use `Cache-Control: private, no-cache` to allow private storage with revalidation, never public/shared caching; return the correct type and `X-Content-Type-Options: nosniff`. Follow existing session-expiry handling through a dedicated image-loading hook using the shared API client and authenticated binary fetch; release temporary browser object URLs when no longer needed and clear images on logout/Recipe change.

List validation follows Ingredient conventions: positive page, page size 1-100 (default 25), search maximum 200 characters. Invalid query returns `400` Problem Details; out-of-range nonempty pages follow the existing Ingredient convention, with page 1 valid for an empty result.

Unauthenticated reads return `401`. Unknown, other-owner and archived Recipe IDs return indistinguishable `404` responses in PRD04. Unexpected failures use centralized Problem Details handling and appropriate server logging.

### Exact search behavior

- Trim the query. Empty/whitespace query lists all active owned Recipes.
- Case-insensitive literal substring match against **Recipe name OR the current name of any linked Ingredient item in any Step**.
- Treat the whole trimmed query as one substring; no tokenization, stemming, fuzzy match or ranking.
- Escape `%`, `_` and backslash using the established PostgreSQL pattern so they are not user-controlled wildcards.
- Use a server-side correlated existence query or equivalent: return each Recipe once, even when multiple items match.
- Include Ingredient items whose Ingredient is archived or grocery-excluded. Exclude Heading/Note text, tags, source, instructions and legacy name snapshots from search.
- Resolve current Ingredient names through the FK so an Ingredient rename affects both display and search without re-import.
- Apply owner and active-Recipe restrictions before counting/paging. Never fetch the whole catalogue and filter in the browser.
- Fixed default order: normalized Recipe name, then Recipe GUID `Id`. This is deterministic paging, not a sorting feature.

Use ordinary EF/PostgreSQL queries first. Do not introduce a search service, denormalized search index or trigram extension without measured need. Index owner/active/name ordering and Ingredient-reference relationships; verify query count does not grow per returned Recipe.

## 11. Recipe List Requirements

- Route `/recipes`, within the existing authenticated shell; document title `Recipe List - Recipe App` using the existing title convention.
- One accessible search box labelled "Search recipes", with helper text explaining name and Ingredient matching.
- A simple single-column list of recipe-name links works on desktop and mobile. No images, editable controls, filter menus or additional card metadata are required.
- Server-side paging, default 25 per page, with existing accessible pagination patterns; no page-size selector is required.
- Keep search and page in URL query state; a changed search resets page to 1. Back from Recipe View restores list search/page.
- Debounce nonempty searches consistently with the Ingredient hook (300 ms), cancel superseded requests, and prevent stale responses replacing current results.
- Distinct initial/search loading, empty catalogue ("No recipes yet"), no search matches, and error-with-retry states. A failed request must not appear as an empty catalogue.
- Recipe links work with keyboard, standard browser navigation and direct URLs. At 375 px and 1280 px widths, no horizontal page overflow or clipped controls.
- No tag/source filters, sorting controls or authoring buttons.

## 12. Recipe View Requirements

- Route `/recipes/:id`, using the Recipe GUID rather than `LegacyId`, without displacing `/recipes/new` or `/recipes/archived`.
- Recipe name as the page heading and document title; Recipe List remains the active navigation item.
- Display source, safe source URL, servings, prep time, cook time, tags and original date-added when present. Optional missing values may be omitted; do not invent zero times or servings.
- Legacy user identity, migration audit fields, original Firebase image URLs and raw provenance are not recipe metadata shown to the user.
- Display the migrated image, when present, above the Steps with responsive width, preserved aspect ratio and concise recipe-based alternative text. Do not crop or transform the original, let the image dominate mobile reading, or request Firebase images.
- Reserve layout space using dimensions. No image is a normal state with no broken element. A failed image request shows an accessible unavailable/retry state while all recipe text remains readable; session expiry uses the existing authentication flow.
- Render Steps in source order with visible Step numbers. Within each Step, render typed items in original order, followed by that Step's instructions.
- Ingredient item: current title-cased Ingredient name, original quantity/unit and notes. No summation, scaling, inferred units or instruction rewriting.
- Heading item: semantic grouping label, not an Ingredient bullet. Note item: distinct readable annotation. Include any nonempty quantity/unit/notes from converted items without conflating them with real Ingredients.
- Preserve multiline instructions as text. Do not interpret source text as executable HTML or introduce rich-text/Markdown parsing.
- Retain instruction-only Steps and item-only Steps; do not display fabricated instructions or "missing ingredient" errors for them. Entirely blank placeholders are not shown.
- Source links are clickable only for validated absolute HTTP/HTTPS URLs. Unsafe/malformed source URLs are presented as non-clickable text with an unavailable-link indication; never fetch URLs server-side.
- Provide loading, error/retry and not-found states. Include a Back to recipes link that retains originating list query state; direct visits fall back to `/recipes`.
- Read-only everywhere: no create, edit, duplicate, delete, archive, restore, favorite or scaling controls. Do not change Ingredient archive state.
- Use shared layout, typography, title casing and modular styles. Validate typed responses with Zod and use dedicated data hooks and the shared API client.

## 13. Acceptance Criteria

Unit = xUnit/FluentAssertions for backend and migration logic, Vitest for frontend logic; BDD = Reqnroll against isolated PostgreSQL; E2E = Playwright. Use synthetic fixtures; primary E2E journeys must include a real seeded API/database path, not only mocked responses.

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-01 | A synthetic Recipe with a non-GUID source ID and GUID Ingredient references | Imported and retrieved | A GUID v7 Recipe Id is assigned, exact source ID is retained as LegacyId, Ingredient GUIDs are unchanged and GUID relationships resolve | Unit + BDD |
| AC-02 | Multiple Steps, repeated Ingredients, ordered tags and structural items | Imported and viewed | Step/item/tag ordering and duplicate occurrences are preserved | BDD + E2E |
| AC-03 | Fractions, ranges, nonnumeric quantities, descriptive servings and times | Imported and viewed | Text is retained without rounding, parsing or scaling | Unit + BDD + E2E |
| AC-04 | Explicit retained Heading and Note decisions | Recipe mode imports matching references | Typed non-Ingredient items are created at their source positions, with all nonempty line fields retained | Unit + BDD |
| AC-05 | A missing Ingredient, unresolved classification or conflicting Heading/Ingredient decision | Write is attempted | Diagnostics identify Recipe/path/reference, exit is nonzero and zero Recipes commit | Unit + BDD |
| AC-06 | A nonblank line with an invalid/missing ID | Validated | It blocks; no name matching or automatic Note fallback occurs | Unit + BDD |
| AC-07 | Entirely blank placeholders, ingredient-free Steps and blank instructions | Imported | Placeholders are retained/reported but not rendered; all Steps remain and valid content is readable | Unit + BDD + E2E |
| AC-08 | Known metadata, legacy departments and an unknown nested field | Imported and round-trip checked | Known fields map; other fields retain exact JSON values in provenance and have reported dispositions | Unit + BDD |
| AC-09 | Both supported date encodings, absent dates and a precision-losing timestamp | Validated | Supported dates preserve UTC value, absent stays null, lossy conversion blocks | Unit + BDD |
| AC-10 | Active, archived and missing-archive source documents | Imported and queried | Archive state persists; missing defaults are reported; only active Recipes appear and archived detail returns 404 | BDD |
| AC-11 | Legacy owner IDs and missing owner fields | Target owner is selected | Explicit selection/confirmation is reported; new owner FK is used; ambiguous multi-owner input blocks | Unit + BDD |
| AC-12 | Ingredient mode and recipe mode inputs | Each mode runs | Existing Ingredient behavior remains; recipe mode reads only explicitly selected relevant files and does not mutate Ingredients | Unit + BDD |
| AC-13 | Valid source and no write flag | Preview runs | Database and object storage are unchanged, local image validation/reporting completes and committed insert/update counts are zero | BDD |
| AC-14 | A source Recipe differs from the target | Operator approves or declines its complete update | Approval applies all approved fields/children atomically; decline leaves the entire Recipe unchanged and is reported | BDD |
| AC-15 | An accepted import already exists | Identical input is imported again | Matching owner/LegacyId resolves the same Recipe GUID, no Recipe/child duplicates or updates occur, all child IDs remain stable, and targets absent from the backup remain untouched | BDD |
| AC-16 | Source, decisions, Ingredient references or Recipe children change after preview | Write is attempted | Conflict blocks commit and requires fresh review; cancellation/failure before commit also writes nothing | BDD |
| AC-17 | A preview, blocked, committed, cancelled or rolled-back run | Report is examined | Every source Recipe has one outcome and its LegacyId, committed mappings include the Recipe GUID, counts reconcile, planned versus committed conversions are separate | Unit + BDD |
| AC-18 | Report persistence fails after successful commit | Tool completes | Nonzero exit explicitly reports committed data and recoverable run ID, never claims rollback | Unit + BDD |
| AC-19 | One Recipe name matches `tofu` and another uses a linked Ingredient named Tofu | User searches `TOFU` | Both appear once; unrelated Recipes do not | BDD + E2E |
| AC-20 | Heading/tag/source/instruction-only matches, duplicate Ingredient matches and `%`, `_`, backslash queries | Search runs | Only literal name/linked-Ingredient matches return, without duplicates or wildcard expansion | Unit + BDD |
| AC-21 | A linked Ingredient is renamed, archived or grocery-excluded | Recipe is searched and viewed | Current name is used; archive/exclusion does not invalidate the reference | BDD |
| AC-22 | Two users and active/archived Recipes | List/detail requests cross ownership boundaries | Data does not leak through results/counts; other-owner/archived/missing details return 404; anonymous requests return 401 | BDD |
| AC-23 | Empty, valid and invalid paging/search inputs | List API is called | Documented DTO shape, fixed ordering, bounds and 400 Problem Details match the contract | Unit + BDD |
| AC-24 | A user changes search rapidly or receives out-of-order responses | Requests complete | Page resets, debounce/cancellation works and stale results never replace current results | Unit |
| AC-25 | A list search/page and an opened Recipe | User returns, refreshes or opens a direct detail URL | Query state and authenticated routing work; reserved stub routes are not captured as IDs | Unit + E2E |
| AC-26 | Loading, empty catalogue, no results, network failure or missing Recipe | Corresponding screen is opened | Distinct accessible states and appropriate retry/navigation are shown; failures are not empty success states | Unit + E2E |
| AC-27 | A Recipe with metadata, headings, notes and multiline instructions | Read at 375 px and 1280 px, including keyboard navigation | All in-scope content is readable in order without horizontal overflow or write controls | E2E |
| AC-28 | HTML-like text, unsafe URLs and original Firebase image URLs | Details render | Text is inert, unsafe URLs are not clickable, and images load only from the authenticated application endpoint, never Firebase | Unit + E2E |
| AC-29 | Names requiring title casing | List and detail render | Shared helper formats names without changing stored names, instructions or labels | Unit + E2E |
| AC-30 | A successful rehearsal against persistent local PostgreSQL | Database restarts without volume removal | Imported Recipes remain accessible; ordered totals/provenance reconcile with the source and report | BDD + Manual rehearsal |
| AC-31 | Synthetic locally created Recipe records with GUID v7 IDs and null LegacyIds | Multiple records for one owner are persisted and a same-named source Recipe is imported | Null LegacyIds are allowed without collisions; import creates a separate mapped Recipe rather than attaching to a null-LegacyId record; no creation endpoint is added | Unit + BDD |
| AC-32 | Exact duplicate, case-distinct and different-owner LegacyIds | Identity constraints and import matching are exercised, including concurrent insertion | Same-owner exact duplicates block/roll back; case-distinct and different-owner values remain distinct; persisted mappings are never reassigned | Unit + BDD |
| AC-33 | A list/detail response and GUID, legacy-string or malformed detail URLs | API and browser clients resolve Recipes | DTO IDs validate as UUIDs and links use GUIDs; legacy strings are not lookup aliases; invalid detail IDs show not-found; reserved stub routes remain intact | Unit + BDD + E2E |
| AC-34 | A source image manifest and valid original raster files | Accepted Recipes are imported twice | Checksums/bytes match source and private target, images associate via LegacyId to Recipe GUID, and the second run reuses associations without duplicate uploads | Unit + BDD |
| AC-35 | A missing/corrupt image, changed checksum, unsupported format or path escaping the backup root | Validation or staging runs | Explicit path/reference diagnostics block all database writes, no unsafe file is opened/uploaded, and no fallback to Firebase occurs | Unit + BDD |
| AC-36 | Images staged before a failed/cancelled/conflicting database commit | Run is reconciled and retried | Existing images remain intact, staged leftovers are journaled and safely reusable/cleanable, and referenced objects are never deleted or reported as rolled back | BDD |
| AC-37 | Private images, two owners, an archived Recipe and anonymous/conditional requests | Image endpoint is requested | Authorization precedes bytes/304; anonymous returns 401, inaccessible/no-image returns 404, and private cache/type headers are correct | BDD |
| AC-38 | A loaded image, absent image, storage outage or expired session | Recipe View renders at 375 px and 1280 px | Responsive aspect ratio/alt text are correct, no-image is normal, failures are explicit with text still readable, and logout/change releases image state | Unit + E2E |
| AC-39 | An existing image and a proposed changed or removed source image | Whole-Recipe update is declined, approved or fails | Decline/failure preserves current association; approval changes it atomically; superseded bytes remain until verified cleanup | BDD |
| AC-40 | Selected free provider and automated cloud image recovery | Storage compatibility and restore rehearsal runs | Private .NET reads/writes work, quota/cost behavior and region are recorded, and database plus images restore with matching associations/checksums without a manually maintained local image collection | BDD + Manual rehearsal |
| AC-41 | A locally running API configured for the development cloud bucket and a verified import | Temporary migration downloads are removed and Recipe View is opened | Images still load through the authenticated API from cloud storage; no local real-image directory or repo copy is required | BDD + Manual rehearsal |
| AC-42 | Synthetic image fixtures and isolated test storage | Automated image tests run without live cloud credentials | Tests pass without accessing real recipe images or a cloud account; fixtures are repository-controlled | Unit + BDD |

## 14. Recommended GitHub Issues & Implementation Sequencing

The following native sub-issues belong to [epic #53](https://github.com/lhjenner/recipe-app-monorepo/issues/53). Each references the relevant PRD sections and reproduces its applicable acceptance criteria in full. Shared criteria appear in multiple issues where different layers provide evidence. Link resulting implementation PRs back to this document and their issue.

| Slice | Recommended issue | Scope / completion evidence | Prerequisites |
|---|---|---|---|
| S1 | [#54 - Recipe persistence and ownership foundation](https://github.com/lhjenner/recipe-app-monorepo/issues/54) | Synthetic fixtures and failing model tests first; GUID Recipe identity, nullable case-sensitive LegacyId and filtered uniqueness, RecipeImage metadata, entities, configurations and EF migration; AC-01/02/03/22/31/32 persistence coverage | PRD03 |
| S7 | [#55 - Private image storage and genuine-free provider compatibility](https://github.com/lhjenner/recipe-app-monorepo/issues/55) | Verify Neon region/plan limits and .NET S3 compatibility; configure development cloud storage and minimal provider/test adapters with synthetic fixtures; document quotas, credentials, automated cloud recovery and go-live transfer; AC-40/41/42 evidence | S1 contract |
| S2 | [#56 - Lossless recipe source parsing and migration planning](https://github.com/lhjenner/recipe-app-monorepo/issues/56) | Source schema fixtures, timestamp variants, decisions, blank handling, full-field dispositions and report contracts; AC-04 through AC-11 parsing coverage | S1 contract |
| S3 | [#57 - Atomic recipe import, repeat runs and durable reporting](https://github.com/lhjenner/recipe-app-monorepo/issues/57) | Extend tool without Ingredient regression; owner/LegacyId matching, stable GUID mappings, approvals, image manifest/staging/journal/recovery, fingerprints/conflicts, provenance, reports and documentation; AC-12 through AC-18, AC-31/32 and AC-34/35/36/39 import coverage | S1, S2, S7 |
| S4 | [#58 - Owner-scoped Recipe read APIs and ingredient-name search](https://github.com/lhjenner/recipe-app-monorepo/issues/58) | GUID DTOs/routes, same-owner active reads, fixed paging, literal search, image metadata and authenticated binary delivery/cache semantics; AC-19 through AC-23, AC-33 API and AC-37 coverage | S1, S7 |
| S5 | [#59 - Responsive read-only Recipe List and View](https://github.com/lhjenner/recipe-app-monorepo/issues/59) | Typed services/hooks, GUID links/validation, metadata/Steps, private image loading and unavailable states, list state and accessibility; AC-24 through AC-29, AC-33 frontend and AC-38 coverage | S4 |
| S6 | [#60 - Migration rehearsal and integrated acceptance](https://github.com/lhjenner/recipe-app-monorepo/issues/60) | Synthetic migration-to-browser journey; actual export/image validation, counts/checksums, persistence restart, database/image backup restore and cutover checklist; AC-30/40 plus cross-slice evidence | S3, S5 |

S2/S3 and S4 may progress in parallel once S1 contracts are agreed. Do not make read API work depend on a personal-data import; use synthetic seeding.

For each slice, write focused failing tests before implementation and keep files within the project's 200-250-line guidance. Extend existing xUnit, Reqnroll, Vitest and Playwright projects rather than introducing a new testing stack. Run focused tests first; retain Ingredient import/read regression coverage for shared-tool/query changes.

### Definition of done

- Acceptance criteria mapped to tests or the explicitly manual rehearsal evidence; existing auth, navigation and Ingredient behavior preserved.
- Local schema applied through EF migrations; importer never applies schema changes itself.
- Same accepted import is idempotent; legacy-to-GUID mappings remain stable and its report reconciles every Recipe and source line.
- Images are privately verified/associated and cloud-authoritative even during local development; failed runs have explicit object recovery, and database-plus-image cloud backups can be restored without manual local synchronization. Free-plan compatibility/limits are recorded; no paid service or self-hosted production dependency is introduced.
- No recipe write endpoints or deferred browsing/filter features have appeared.
- Tool guide documents new mode/parameters, report recovery, ownership confirmation and lossless-source retention; final issue/PR links replace traceability placeholders.

## 15. Risks, Assumptions & Future Considerations

| Risk / assumption | Response |
|---|---|
| Classification decisions and matching ingredient catalogue have not been verified | Require them during rehearsal; do not equate GUID syntax with referential integrity |
| Original structural text may be absent from a recipe line | Require authoritative legacy ingredient source for fallback; a decision review label is insufficient |
| Legacy owner fields are sparse | Use explicit destination-owner selection and confirmation; retain original values privately |
| Legacy Recipe references still use strings while the new domain uses GUIDs | Keep immutable, owner-scoped LegacyId mappings; future domain imports resolve them to GUID FKs and block missing mappings rather than minting replacement Recipes |
| An archived Recipe exists but browsing is deferred | Import and retain it; active APIs exclude it; existing Archived Recipes stub remains unchanged |
| Images contain remote access-bearing URLs | Back up original files through an explicit export; retain URLs privately, migrate verified bytes to private storage, and serve only after Recipe ownership checks |
| Object storage is not transactional with PostgreSQL | Immutable staged objects, atomic database associations, durable run journals, conservative reference-checked cleanup and explicit uncertain-commit reconciliation |
| Free-plan terms, bandwidth or compute can constrain service | Verify limits/region and exhaustion behavior before choosing Neon; prefer stopping over charges, monitor usage, avoid keep-alives and retain portable backups |
| Cloud storage alone may not protect against deletion, and free recovery features are unverified | Validate automated cloud recovery and matching database associations; record any cost/retention gap for decision before cutover, rather than relying on a manually maintained local image collection |
| Text values cannot support reliable arithmetic | Preserve text now; future scaling/nutrition requires explicit parsing, units and human review |
| Ingredient names can change | Link by GUID and display/search the current name; retain imported snapshots for provenance |
| Unknown metadata or legacy department copies carry meaning | Retain and report; never override current Ingredient mappings; review newly discovered meaningful fields before cutover |
| Source JSON is not a byte archive | Retain checksum-verified original files separately; do not claim JSONB preserves source bytes |
| Post-approval concurrency and whole-run transactions can contend | Keep prompts outside locks, use bounded acquisition and explicit retries from a fresh preview |
| Future editing conflicts with positional import matching | Establish an authoring/import cutoff or explicit reconciliation before enabling local recipe edits or downstream Step references |
| Database/report outcomes can diverge | Preflight report storage, retain commit/run provenance and surface post-commit reporting failure honestly |
| Catalogue/search volume may grow | Measure real workloads before introducing indexes/extensions beyond ordinary relational needs; no unverified latency promise |
| Source content may be rich text in future exports | Retain it and render safely as text until a separate reviewed rendering requirement exists |

### Rehearsal and cutover

1. Back up the legacy export, classification decisions and destination database outside the repository; verify destination account and applied schema.
2. Dry-run Recipe mode and resolve every blocker. Review warnings, archive defaults, blank placeholders, retained fields, source ownership and proposed changes.
3. Apply an explicit write run; review the persistent report and verify active Recipe list/search/detail against representative source structures.
4. Rerun identical accepted input, confirm no changes, and restart persistent local PostgreSQL without removing its volume.
5. While the legacy app remains active, repeat against fresh backups. Before final cutover, stop legacy writes, take a final export and resolve required declines/blockers.
6. Reconcile image references, manifest files, stored checksums and Recipe associations, including archived Recipes. Verify private image display from the local API against development cloud storage and an automated cloud database-plus-image recovery rehearsal. Preserve original Firebase files until verification; temporary local downloads may be removed after verified import and backup. Review staged/superseded objects before explicit cleanup. Archived browsing remains deferred.
7. Production hosting, restricted connectivity and secret injection remain separate prerequisites for production cutover. Verify genuine-free plan/quota behavior and private storage compatibility; do not describe migration as complete with unresolved image failures, unreviewed content or missing references.

Keep the migration tool until the later Grocery List migration and final verification, consistent with PRD03. Removing the temporary executable must not delete retained recipe provenance, authoritative cloud image objects, manifests, recoverable original backups, decisions or final reports. This does not require retaining temporary local image downloads or manually synchronizing image copies. Sharing, archive/restore UX, image authoring/transformations, recipe authoring, filtering and downstream integrations require later PRDs; this schema supports them without implementing them prematurely.
