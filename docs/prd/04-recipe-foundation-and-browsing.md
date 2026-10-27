# PRD 04: Recipe Foundation, Migration & Read-Only Browsing

## Status & Traceability

- Status: Proposed; grounded in the current implementation and a supplied legacy export.
- Epic, implementation issues and pull requests: Not created. Recommended issue boundaries and dependencies appear below; replace these placeholders with actual links when created.
- Depends on: [PRD01 - Authentication & Login](./01-authentication-and-login.md), [PRD02 - App Shell & Navigation](./02-app-shell-and-navigation.md), and [PRD03 - Ingredient Maintenance](./03-ingredient-maintenance.md).
- Reuses the Ingredient foundation from [epic #39](https://github.com/lhjenner/recipe-app-monorepo/issues/39), particularly the migration tool introduced by [issue #42](https://github.com/lhjenner/recipe-app-monorepo/issues/42).
- Each implementation issue and PR must link this PRD and identify its acceptance criteria. No issue number in this document implies that PRD04 work has already been created or completed.

## 1. Overview & Problem Statement

The rewritten application has a populated, user-owned Ingredient catalogue but no Recipe domain. Recipes are the next foundation for Grocery Lists, Meal Planning and Food Diary. Migrating recipes must retain the existing Ingredient GUID references and the legacy step-based structure without encoding headings and notes as fake Ingredients.

PRD04 establishes durable Recipe persistence, extends the standalone import tool, and replaces the Recipe List stub with lean, read-only browsing and detail screens. It does not introduce recipe authoring or downstream integrations.

**Storage clarification:** Firestore is the legacy source, exported as JSON. PostgreSQL through EF Core remains the destination. Firestore recommendations in this PRD describe source interpretation and mapping, not a second application database or a Firestore redesign.

## 2. Scope, Goals & Non-Goals

### In scope

- User-owned Recipes with ordered Steps and ordered, typed Step Items.
- Preservation of Recipe IDs, Ingredient references, metadata, source content and ordering.
- Import of active and archived recipes, preserving their archive state.
- Explicit conversion of legacy structural ingredient entries to Heading or Note items using retained PRD03 decisions.
- Lossless source retention, validation, repeat imports and durable migration reports.
- Authenticated active Recipe List, recipe-name/ingredient-name search, and read-only Recipe View.
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
- Image display, image-file migration, image upload or image hosting.
- In-app import/upload endpoints, production provisioning or changes to authentication.
- A new Firestore target model, live Firestore access, synchronization or dual writes.

The existing Add Recipe and Archived Recipes navigation destinations remain stubs. Preserve the app shell and menu ordering; do not make those destinations functional as an incidental part of this PRD.

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
| Line fields | `id`, `name`, `quantity`, `unit`; optional `notes` and legacy `department` |
| Free text | Quantities include fractions, ranges, embedded units and a nonnumeric label; servings and times also contain descriptive text |
| Structural labels | Examples include Optional, Setup, Full Recipe, Half Recipe and serving/topping labels |

No live database, retained classification file or matching ingredient export was supplied for this inspection. This PRD does **not** claim all Ingredient references resolve, that all structural labels are classified, or that import is ready to write.

## 4. User Stories

- As a user, I want my existing recipes migrated with their original identities so that future references remain stable.
- As a user, I want each recipe's steps and ingredient lines kept in order so that I can follow the original method.
- As a user, I want headings and notes displayed as structured content so that serving suggestions are not mistaken for ingredients.
- As a user, I want to search by recipe name or an ingredient's name so that I can find meals using that ingredient.
- As a user, I want to read recipe metadata and instructions on any device so that I can use the recipe while cooking.
- As a migration operator, I want explicit validation and reconciliation reports so that I can resolve problems before switching applications.
- As a future feature developer, I want stable Recipe, Step and Ingredient references so that authoring and downstream features can build on this foundation.

## 5. Domain Model Recommendations & Entity Definitions

### Aggregate and identity

Use `Recipe -> RecipeStep -> RecipeStepItem`, with Ingredient items referencing the existing Ingredient entity. Tags belong to the Recipe; source is recipe metadata, not a new managed taxonomy.

- Recipe IDs are opaque, case-sensitive strings. Preserve the exported `id` exactly; do not generate GUID replacements, lowercase it, use the title as a key or introduce an unnecessary ID mapping table.
- Use a PostgreSQL `text` key with deterministic, case-sensitive equality. Do not constrain the model to exactly 20 characters merely because this sample uses that format.
- New internal Step, Step Item and tag-row IDs can use GUIDs. Imported Steps/items have no source IDs.
- Persist an explicit zero-based `Position` for Steps, items and tags. Enforce uniqueness within the parent. Do not rely on insertion order, dictionary order or primary-key order.
- Keep duplicate ingredient occurrences and tags, including repeated use of the same Ingredient in one Step. Do not aggregate quantities or deduplicate source arrays.
- Do not impose Ingredient-style recipe-name uniqueness: a recipe's identity is its ID, not its name.

### Recommended entities

| Entity | Fields / relationships | Rules |
|---|---|---|
| Recipe | `Id: string`, `OwnerUserId: Guid`, `Name: string`, `NormalizedName: string`, `SourceText`, `SourceUrl`, `ServingsText`, `PrepTimeText`, `CookTimeText`, `ImageUrl`, `DateAddedAtUtc?`, `IsArchived: bool`, application `CreatedAtUtc` / `UpdatedAtUtc`, `Version` | Owner FK to existing User; strings remain source text; original date is distinct from import/audit time; `xmin` supports import conflict detection |
| RecipeStep | `Id: Guid`, `RecipeId`, `Position: int`, `Instructions: string` | One Recipe; ordered; instructions may be empty; retain line breaks; ingredient-free Steps are valid |
| RecipeStepItem | `Id: Guid`, `RecipeStepId`, `Position: int`, `Kind: Ingredient / Heading / Note`, `IngredientId: Guid?`, `Text: string`, `QuantityText`, `UnitText`, `NotesText` | Shared ordered list; Ingredient requires a real FK; Heading/Note require null Ingredient FK; never create fake Ingredient rows |
| RecipeTag | `Id: Guid`, `RecipeId`, `Position: int`, `Text: string` | Ordered source values, not a global tag catalogue; no tag-management behavior |
| RecipeImportProvenance | Recipe FK, source document JSON, document SHA-256, source-file SHA-256, mapping-version identifier, decision fingerprint, last applied run identifier/time | Non-public persistence concern, separate from the authoring domain; contains enough source detail to verify every mapping and omission |

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

Do not modify the legacy Firestore collections to make this import work. Consume the supplied JSON-array export through an explicit recipe import mode; no Firestore credentials or SDK are required.

| Legacy JSON path | Destination / interpretation |
|---|---|
| `id` | Exact Recipe string primary key |
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
| `imageUrl` | Retained value only; no image requests or file migration in PRD04 |
| `userId` | Legacy provenance only; never used as the new application's owner FK |
| Any unrecognized property | Preserve in source JSON and emit a field-path warning; never silently ignore |

Recognize both observed timestamp shapes: `_seconds` / `_nanoseconds` and `seconds` / `nanoseconds` with `type: "firestore/timestamp/1.0"`. Validate seconds, nanosecond range and destination precision. Block unsupported or lossy typed conversion instead of rounding; retain the source in all cases. Missing `dateAdded` stays null.

The source `id` is the exported recipe identifier. Unlike Ingredients, it is not a GUID. There is no evidence here requiring a new Firestore document hierarchy, subcollection conversion or new source ID scheme.

## 7. Recipe Import Strategy

### Extend, do not replace, PRD03 tooling

Add an explicit recipe mode to [FirestoreMigration](../../tools/FirestoreMigration/README.md). Preserve the current Ingredient invocation and behavior unchanged. Recipe mode must not import Ingredients, users or unrelated exports as a side effect.

Reuse JSON duplicate-key checks, safe configuration, owner validation, schema checks, dry-run planning, complete diffs, individual approvals, cancellation, input fingerprints and post-approval conflict detection. Extend source selection, planning, writing, target snapshots and reporting only where Recipes require it.

### Inputs and ownership

- Explicit recipe JSON path, existing destination owner GUID, retained PRD03 decision file, and report destination outside the repository.
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
- An explicit write run makes a fresh preview. New Recipes require no per-recipe approval; changed Recipes require approval of the entire Recipe, including metadata, children and provenance changes.
- Unchanged Recipes are skipped. Declined updates leave the complete target Recipe unchanged and remain proposed on a later run.
- Never delete destination Recipes absent from the source export.
- Preserve Recipe ID, owner and existing application creation time on updates. Apply the source archive state as an explicitly displayed, approved field change.
- Source changes to arrays, unknown fields or other provenance content must not be hidden by a typed-field-only comparison. JSON object-property order alone does not constitute a content change; array order does.
- Commit all accepted changes in one transaction. Any blocking validation or write failure means zero accepted Recipes are committed; this PRD does not introduce partial-success batches.
- After approvals, recheck all selected input fingerprints and the relevant target read-set, including referenced Ingredients, classifications, Recipe children and concurrent insertions. Hold verified inputs through commit and use bounded locking consistent with PRD03. Do not hold database locks while prompting.
- Cancellation before commit rolls back all writes. Operational failures must be explicit, not disguised as successful zero-count imports.
- Repeat the same accepted input: zero inserted/updated Recipes, unchanged child IDs, and no duplicate tags, Steps or items.

### Losslessness contract

Every source field must be either represented in the typed model or retained in per-recipe source JSON with a reported disposition. Keep original backup bytes separately; PostgreSQL JSON storage is not a byte-for-byte archive and does not preserve object-key order or lexical number formatting.

Unknown metadata and legacy-only fields are retained even if not yet rendered. This is not permission to hide a required user-facing field: all known recipe metadata in scope must be mapped and displayed. Newly discovered meaningful fields require a documented mapping decision before cutover.

Report and source files can contain personal data or access-bearing URLs. Keep them outside version control with restricted access; redact credentials, URL query tokens and legacy user identifiers from ordinary console output. Never commit the supplied export or use it directly in automated tests.

## 8. Migration Validation Rules

| Area | Blocking condition | Valid/warning condition |
|---|---|---|
| JSON | Malformed input, duplicate object keys, wrong root/field types | Unknown fields retained with field-path warnings |
| Recipe identity | Blank/duplicate ID, ownership conflict, unsupported route-safe representation without a defined encoding strategy | Preserve opaque string IDs; duplicate names are allowed |
| Recipe content | Missing/blank title; missing or non-array Steps; non-string instructions; malformed tags/items | Empty arrays, instructions and optional metadata remain valid |
| Ingredient identity | Nonblank invalid GUID, missing or different-owner Ingredient | Archived and grocery-excluded Ingredients remain usable |
| Classification | Missing required decision, invalid classification, contradictory Ingredient row, lost structural text | Explicit Heading/Note decisions create typed non-Ingredient items |
| Line content | Unexpected field types or nonempty unresolvable lines | Fractions, ranges, embedded units and unusual text retained with anomaly warnings |
| Placeholders | Blank ID with any meaningful content or unknown nonempty field | Entirely blank object retained and omission counted |
| Ordering | Duplicate generated parent/position keys or changed order during mapping | Empty Steps and repeated Ingredient occurrences preserved |
| Dates | Invalid timestamp shape/range or precision loss in typed conversion | Missing original date stays null; supported shapes normalize to UTC |
| Archive/owner | Nonboolean archive value or unapproved source-owner selection | Missing archive defaults to active with diagnostic; missing legacy owner reported |
| URLs | Non-string source/image value | Blank allowed; malformed/unsafe URLs retained with warning but never made clickable |
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

### Per-recipe diagnostics

- Recipe ID, outcome, ordered field diffs, error/warning code, JSON field path, source Step/item positions and affected reference GUID where appropriate.
- Preserve JSON-path context such as `steps[2].ingredients[1]`; human labels may additionally use one-based Step numbers.
- Distinguish invalid source, dependency missing, declined, unchanged, not applied due to another blocker, and rolled back. Do not label every uncommitted Recipe "failed".
- Counts must reconcile: every source Recipe has exactly one terminal outcome. Dry-runs report zero committed imports; rolled-back runs report zero committed inserts/updates.
- Record ordered source Step/item totals and dispositions, enabling verification that each source item became an Ingredient, Heading, Note or retained blank placeholder.

Prepare a writable report destination before write mode starts. Filesystem reports and database transactions are not one atomic resource: retain a run/commit identifier with applied provenance and record the commit outcome explicitly. If final report persistence fails after a database commit, exit nonzero, state that the data **was committed**, and provide recovery instructions/run identification; do not claim rollback or encourage a blind retry.

Reuse exit codes `0` for successful preview/write, `1` for blocking/operational failure, and `2` for cancellation. Persistent reports must not overwrite earlier runs unintentionally.

## 10. Read API & Search Requirements

Follow the existing Controller -> application read service -> EF persistence pattern. Return dedicated DTOs, not entities or raw provenance. Use async I/O and pass CancellationTokens throughout.

| Endpoint | Contract |
|---|---|
| `GET /api/recipes?search=&page=1&pageSize=25` | Active, current-owner Recipes only; `items`, `totalCount`, `page`, `pageSize`; summaries need only ID and name |
| `GET /api/recipes/{id}` | Current-owner, active Recipe metadata, ordered tags, ordered Steps and discriminated typed items; includes current Ingredient names and preserved line text |

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
- Fixed default order: normalized Recipe name, then case-sensitive Recipe ID. This is deterministic paging, not a sorting feature.

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

- Route `/recipes/:id`, without displacing `/recipes/new` or `/recipes/archived`.
- Recipe name as the page heading and document title; Recipe List remains the active navigation item.
- Display source, safe source URL, servings, prep time, cook time, tags and original date-added when present. Optional missing values may be omitted; do not invent zero times or servings.
- Legacy user identity, migration audit fields and raw provenance are not recipe metadata shown to the user. Image URLs are retained but not rendered as images or raw token-bearing text.
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
| AC-01 | A synthetic Recipe with a non-GUID string ID and GUID Ingredient references | Imported and retrieved | Recipe ID and Ingredient GUIDs are unchanged and relationships resolve | Unit + BDD |
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
| AC-13 | Valid source and no write flag | Preview runs | Database is unchanged, persistent report exists and committed insert/update counts are zero | BDD |
| AC-14 | A source Recipe differs from the target | Operator approves or declines its complete update | Approval applies all approved fields/children atomically; decline leaves the entire Recipe unchanged and is reported | BDD |
| AC-15 | An accepted import already exists | Identical input is imported again | No Recipe/child duplicates or updates occur, all child IDs remain stable, and targets absent from the backup remain untouched | BDD |
| AC-16 | Source, decisions, Ingredient references or Recipe children change after preview | Write is attempted | Conflict blocks commit and requires fresh review; cancellation/failure before commit also writes nothing | BDD |
| AC-17 | A preview, blocked, committed, cancelled or rolled-back run | Report is examined | Every source Recipe has one outcome, counts reconcile, planned versus committed conversions are separate | Unit + BDD |
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
| AC-28 | HTML-like text, unsafe URLs and retained image URLs | Details render | Text is inert, unsafe URLs are not clickable, and no image request is made | Unit + E2E |
| AC-29 | Names requiring title casing | List and detail render | Shared helper formats names without changing stored names, instructions or labels | Unit + E2E |
| AC-30 | A successful rehearsal against persistent local PostgreSQL | Database restarts without volume removal | Imported Recipes remain accessible; ordered totals/provenance reconcile with the source and report | BDD + Manual rehearsal |

## 14. Recommended GitHub Issues & Implementation Sequencing

These are proposed issue titles, not instructions to create issues automatically. Create one PRD04 epic and link the implementation issues and resulting PRs back to this document.

| Slice | Recommended issue | Scope / completion evidence | Prerequisites |
|---|---|---|---|
| S1 | Recipe persistence and ownership foundation | Synthetic fixtures and failing model tests first; entities, configurations, constraints and EF migration; AC-01/02/03/22 persistence coverage | PRD03 |
| S2 | Lossless recipe source parsing and migration planning | Source schema fixtures, timestamp variants, decisions, blank handling, full-field dispositions and report contracts; AC-04 through AC-11 parsing coverage | S1 contract |
| S3 | Atomic recipe import, repeat runs and durable reporting | Extend tool without Ingredient regression; approval/diffs, fingerprints/conflicts, provenance, report recovery, documentation and rehearsal instructions; AC-12 through AC-18 | S1, S2 |
| S4 | Owner-scoped Recipe read APIs and ingredient-name search | DTOs, query validation, same-owner active reads, fixed paging, literal search and database-backed scenarios; AC-19 through AC-23 | S1 |
| S5 | Responsive read-only Recipe List and View | Typed services/hooks, route replacement, metadata and Step rendering, list state, accessibility and safe URLs; AC-24 through AC-29 | S4 |
| S6 | Migration rehearsal and integrated acceptance | Realistic synthetic migration-to-browser journey; operator-run actual export validation, count reconciliation, persistence restart and cutover checklist; AC-30 plus cross-slice evidence | S3, S5 |

S2/S3 and S4 may progress in parallel once S1 contracts are agreed. Do not make read API work depend on a personal-data import; use synthetic seeding.

For each slice, write focused failing tests before implementation and keep files within the project's 200-250-line guidance. Extend existing xUnit, Reqnroll, Vitest and Playwright projects rather than introducing a new testing stack. Run focused tests first; retain Ingredient import/read regression coverage for shared-tool/query changes.

### Definition of done

- Acceptance criteria mapped to tests or the explicitly manual rehearsal evidence; existing auth, navigation and Ingredient behavior preserved.
- Local schema applied through EF migrations; importer never applies schema changes itself.
- Same accepted import is idempotent and its report reconciles every Recipe and source line.
- No recipe write endpoints or deferred browsing/filter features have appeared.
- Tool guide documents new mode/parameters, report recovery, ownership confirmation and lossless-source retention; final issue/PR links replace traceability placeholders.

## 15. Risks, Assumptions & Future Considerations

| Risk / assumption | Response |
|---|---|
| Classification decisions and matching ingredient catalogue have not been verified | Require them during rehearsal; do not equate GUID syntax with referential integrity |
| Original structural text may be absent from a recipe line | Require authoritative legacy ingredient source for fallback; a decision review label is insufficient |
| Legacy owner fields are sparse | Use explicit destination-owner selection and confirmation; retain original values privately |
| An archived Recipe exists but browsing is deferred | Import and retain it; active APIs exclude it; existing Archived Recipes stub remains unchanged |
| Images contain remote access-bearing URLs | Preserve privately; no rendering, availability checks or assumed transfer of image ownership; later PRD must address storage/access and expiry |
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
6. Preserve archived records and image URLs even though their browsing/display is deferred. Do not describe Recipe migration as complete if unreviewed content or missing references remain.
7. Production hosting, restricted connectivity, secret injection and backup/restore remain separate prerequisites for production cutover.

Keep the migration tool until the later Grocery List migration and final verification, consistent with PRD03. Removing the temporary executable must not delete retained recipe provenance, original backups, decisions or final reports. Sharing, archive/restore UX, images, authoring, filtering and downstream integrations require later PRDs; this schema supports them without implementing them prematurely.
