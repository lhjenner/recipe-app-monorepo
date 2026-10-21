# PRD 03: Ingredient Maintenance

## Traceability

- Epic: [#39 - Ingredient Maintenance (PRD03)](https://github.com/lhjenner/recipe-app-monorepo/issues/39)
- Stories (one per implementation slice - see [Implementation Slices](#implementation-slices)):
  - [#40 - Persistence foundation and lookup APIs](https://github.com/lhjenner/recipe-app-monorepo/issues/40)
  - [#41 - Ingredient schema, read APIs and responsive maintenance list](https://github.com/lhjenner/recipe-app-monorepo/issues/41)
  - [#42 - Standalone Firestore ingredient migration tool](https://github.com/lhjenner/recipe-app-monorepo/issues/42)
  - [#43 - Create and edit ingredients with validation and concurrency](https://github.com/lhjenner/recipe-app-monorepo/issues/43)
  - [#44 - Archive and restore ingredients](https://github.com/lhjenner/recipe-app-monorepo/issues/44)
- Pull Requests: _TBD_
- Depends on: [PRD 01 — Authentication & Login](./01-authentication-and-login.md), [PRD 02 — App Shell & Navigation](./02-app-shell-and-navigation.md)

## Overview & Problem Statement

Ingredients are the foundational domain of the app. Recipes, Grocery Lists, the Meal Planner and the Food Diary all depend on them, either directly or through Recipes. The existing ingredient data (312 ingredients, plus categories, departments and supermarket mappings) lives in the old Firestore app and must move into the new system **without changing ingredient identifiers**, because recipe lines and grocery list items in Firestore reference ingredients by ID.

The old app also has an `invalid` flag that conflates two different concepts:

- **Real ingredients that should never appear on a grocery list** (e.g. water, pasta water).
- **Structural labels** that were stored as fake ingredients so they could appear in recipe ingredient lists (e.g. "serving suggestions:", "optional:", "setup").

This PRD establishes the Ingredient domain, its persistence, a CRUD API, a standalone temporary Firestore migration tool, and a basic maintenance screen. It replaces the `invalid` concept with an explicit `ExcludeFromGroceryList` flag for real ingredients and keeps headings and notes out of the Ingredient table entirely. The old app remains in use during migration, so the tool supports repeat imports with automatic additions and explicitly approved updates.

The goal is **not** the final ingredient management experience. It is a clean, durable domain model and enough UI to validate imported data and maintain individual ingredients.

**Explicitly out of scope** (deferred to future PRDs):

- Recipe functionality of any kind, including recipe import
- Nutrition calculations (recipe nutrition, food diary, calorie summaries)
- Grocery list behaviour (this PRD stores `ExcludeFromGroceryList`; the Grocery List PRD enforces it)
- Supermarket / store management (stores and chains are seeded, read-only reference data)
- Category and department management screens (rename, delete, merge); only list + create-on-the-fly is included
- Bulk editing, advanced filtering, column visibility or reordering, saved views
- Desktop table/card view toggle
- Ingredient health / data-quality dashboards, duplicate detection tools
- Pricing (prices belong to each supermarket; this app has no awareness of pricing)
- Product link reordering UI (links keep the order in which they were imported or added)
- Sharing ingredients between user accounts
- In-app import pages, upload endpoints, staging tables and import feature flags
- Production hosting and database provisioning (including deployment networking, secrets and backup infrastructure); these are required before production cutover, not before a local import

## Design Decisions

These decisions were made before implementation because they are expensive to change once Recipe migration begins.

| ID | Decision | Rationale |
|---|---|---|
| D1 | **Ingredients, Categories and Departments are owned by a user** (`OwnerUserId`). Another user's data returns `404`, never `403`. | Registration is open (PRD 01), so a global catalogue would let any new account edit or archive the real data. Costs one FK and a repository filter now; retrofitting after Recipes exist would require a backfill across every table. Sharing between accounts is not planned. |
| D2 | **One `AppDbContext`** (renamed from `AuthDbContext`), with one `IEntityTypeConfiguration<T>` class per entity. | Future Recipe entities need FKs to Ingredients (and Ingredients need an FK to Users). Separate contexts make cross-feature FKs and migration ordering awkward. |
| D3 | **Imported ingredients keep their Firestore GUID as their primary key.** New ingredients get a Guid v7. No `LegacyId` column and no mapping table. | Every Firestore ingredient has a valid, unique GUID in its `id` field, and every recipe line and grocery list item references ingredients by that GUID. Reusing it means future imports need no lookup. Note: the Firestore **document key** is the ingredient name; the import must use the `id` field, never the document key. |
| D4 | **Persist migration classifications by legacy ingredient GUID:** `Ingredient`, `Heading` or `Note`. Each `invalid: true` item requires an explicit decision in a separate migration decision file. | `Ingredient` imports with `ExcludeFromGroceryList = true`; `Heading` and `Note` create no Ingredient. Future Recipe migration uses the recorded decision, never infers a heading from a missing database row. The decision file is migration metadata, not an ID mapping table or part of the core domain. |
| D5 | **Archive and restore only; no hard delete.** | Hard-deleting an imported ingredient before Recipe import would orphan recipe lines. Archived ingredients still count toward name uniqueness. |
| D6 | **Ingredient names are unique per owner** (case-insensitive, trimmed, internal whitespace collapsed). | Prevents ambiguous ingredient lookup in recipes and grocery lists. The export contains no duplicates under this rule. |
| D7 | **`SupportsBothPricingMethods` is a boolean on each product link**, not on the ingredient. | In the source data it lives on individual Woolworths product entries (e.g. Granny Smith vs Royal Gala apples) and is only ever `true` or absent. A richer weight-only / quantity-only model would be guessing; defer until the Grocery List PRD needs it. |
| D8 | **Nutrition is stored per 100 g with units in the column names** (`EnergyKcal`, `SodiumMg`, all others in grams), as nullable `numeric` values. `null` means unknown. | Verified against source data (all-purpose flour: 349 kcal, 4 mg sodium). Fixed-point decimals avoid floating-point drift in future calculations. |
| D9 | **Categories and Departments are user-owned entities** referenced by FK. Departments are one list shared by all stores. | The old app lets users manage both lists. They have no Firestore IDs, so new IDs are generated and the import matches them by name. |
| D10 | **A store mapping is one of three states:** *mapped* (has a department), *not stocked* (explicitly unavailable at that store), or *unmapped* (no row). | The source data distinguishes `"n/a"` (deliberately not stocked, 13 mappings) from `""` (41 values) and missing store keys (108), both of which mean not yet mapped. Conflating them would hide real data gaps. |
| D11 | **Supermarket chains and stores are seeded reference data** with stable codes matching Firestore (`woolworths-carlyle`, etc.) and fixed seeded IDs. | Store management is deferred. Stable codes let the import map Firestore keys without configuration. |
| D12 | **One `ExcludeFromGroceryList` flag.** | Excluding from grocery lookup and from grocery generation are two uses of the same fact. Named for its behaviour, not "invalid". |
| D13 | **Optimistic concurrency** using PostgreSQL `xmin`. A stale update returns `409`, and the UI keeps the user's typed values. | One account is shared by two people on multiple devices; last-write-wins would silently lose edits. |
| D14 | **A standalone migration tool defaults to dry-run.** An explicit write run inserts new ingredients without per-item prompts, skips unchanged ones, and prompts to accept or decline each changed ingredient after showing its complete diff. | No app import UI, endpoints or staging tables. Repeat imports support continued use of the old app. Accepted writes are transactional; input or target changes invalidate the reviewed plan. No automatic deletions or archive changes are inferred. |

## User Stories

- As a user, I want my existing ingredients imported with their original IDs so that my recipes and grocery lists can be migrated later without broken references.
- As a user, I want to preview what an import will do before anything is saved so that I can catch problems in the source data first.
- As a user, I want to record, item by item, whether old "invalid" ingredients are real ingredients, headings or notes so that later Recipe migration preserves my decisions.
- As a user, I want to import newer backups, automatically adding new ingredients and reviewing proposed updates before accepting them so that continued use of the old app does not silently overwrite changes in the new app.
- As a user on desktop, I want to see every ingredient's category and department at each supermarket in one table so that I can spot missing or wrong mappings without opening each record.
- As a user, I want to search and page through ingredients so that I can find one quickly.
- As a user, I want to create and edit an ingredient's name, category, store departments, product links, nutrition and grocery-list exclusion so that my master data stays accurate.
- As a user, I want to archive an ingredient I no longer use, and restore it later, so that my list stays tidy without losing references.
- As a user on mobile, I want to search and edit a single ingredient while shopping so that I can correct a department on the spot.
- As a user, I want to be warned if someone else changed an ingredient while I was editing it so that neither of us silently overwrites the other.

## Acceptance Criteria

Each criterion is a Given/When/Then scenario. The **Test Type** column declares which layers verify it (Unit = xUnit for API or Vitest for web, BDD = Reqnroll, E2E = Playwright).

### Ownership & isolation

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-01 | A logged-out client | It calls any ingredient, category, department or supermarket endpoint | `401` is returned | BDD |
| AC-02 | User A owns an ingredient | User B requests, updates, archives or restores it by ID | `404` is returned and nothing changes | BDD |
| AC-03 | Users A and B each have ingredients, categories and departments | Either user lists or searches them | Only their own records are returned | BDD |
| AC-04 | User A has an ingredient named "Onion" | User B creates "Onion" | It succeeds; uniqueness is per owner | BDD |

### Ingredient list & search

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-05 | A user with more ingredients than one page | They open Ingredient Maintenance | Active ingredients are shown sorted by name (case-insensitive), with page controls and a total count; default page size 25 | BDD + E2E |
| AC-06 | The list is shown | The user types in the search box | Results are filtered server-side to names containing the text (case-insensitive), the page resets to 1, and the request is debounced | BDD + E2E |
| AC-07 | A search matches nothing | Results load | An empty state "No ingredients match '{term}'" is shown | E2E |
| AC-08 | A desktop-width viewport (above 700px) | The list loads | A table shows columns: Ingredient, Category, one column per store in seeded display order, Actions. Store columns come from the API, not hardcoded | E2E |
| AC-09 | An ingredient with mapped, not-stocked and unmapped stores, and no category | It appears in the table | Mapped cells show the department name; not-stocked cells show "Not stocked"; unmapped cells and the missing category show "Missing" with a distinct warning style (text, not colour alone) | Unit + E2E |
| AC-10 | An ingredient with `ExcludeFromGroceryList = true` | It appears in the list | It shows an "Excluded from grocery list" badge next to its name | E2E |
| AC-11 | A mobile-width viewport (700px or below) | The list loads | Ingredients appear as cards (name, category, each store's mapping state, exclusion badge); no table is rendered; search and paging work as on desktop | E2E |
| AC-12 | The "Show archived" toggle is on | The list loads | Only archived ingredients are shown, each with a Restore action instead of Archive | BDD + E2E |
| AC-13 | A page number beyond the last page, or a page size outside 1–100 | The API is called | `400` Problem Details is returned | BDD |

### Create & edit

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-14 | The list page | The user selects "Add ingredient" | They go to `/ingredient-maintenance/new` with an empty form | E2E |
| AC-15 | A valid new ingredient | It is saved | `201 Created` with a `Location` header; the new ID is a Guid v7; the user returns to the list and sees it | BDD + E2E |
| AC-16 | A card or table row | The user selects Edit (desktop) or the card (mobile) | They go to `/ingredient-maintenance/{id}` with the form populated | E2E |
| AC-17 | A valid edit | It is saved | `200 OK` with the updated ingredient and a new `version`; the list reflects the change | BDD + E2E |
| AC-18 | A name that is blank, or over 200 characters after trimming | Saved | `400` Problem Details with a field error; the form shows it next to the Name field | Unit + BDD + E2E |
| AC-19 | A name that matches another **active** ingredient of the same owner (case-insensitive, whitespace-normalised) | Saved | `409` Problem Details "An ingredient with this name already exists" | BDD + E2E |
| AC-20 | A name that matches an **archived** ingredient of the same owner | Saved | `409` Problem Details including the archived ingredient's ID; the form shows "An archived ingredient with this name exists" with a Restore action | BDD + E2E |
| AC-21 | Leading, trailing or repeated internal whitespace in a name | Saved | It is stored trimmed with internal whitespace collapsed; letter case is preserved | Unit + BDD |
| AC-22 | The form's Category field | The user types | Existing categories matching the text are suggested; if no exact match exists, a "Create '{text}'" option creates it via `POST /api/categories` and selects it | E2E |
| AC-23 | The form's department field for a store | The user types | Existing departments are suggested, with the same "Create '{text}'" behaviour via `POST /api/departments` | E2E |
| AC-24 | A store row in the form | The user ticks "Not stocked" | The department field is cleared and disabled; saving stores a not-stocked mapping | Unit + BDD + E2E |
| AC-25 | A store row with no department and "Not stocked" unticked | Saved | No mapping exists for that store (unmapped) | BDD |
| AC-26 | A mapping or product link referencing an unknown store, chain, category or department ID, or one owned by another user | Saved | `400` Problem Details; nothing is saved | BDD |
| AC-27 | The Product links section | The user adds a link (chain, URL, optional variant label, optional note, "Supports both pricing methods", "Preferred") or removes one | Links are saved in the order shown | BDD + E2E |
| AC-28 | A product link URL that is not an absolute `https` URL | Saved | `400` Problem Details with a field error | Unit + BDD |
| AC-29 | More than one link marked Preferred for the same chain | Saved | `400` Problem Details | Unit + BDD |
| AC-30 | The Nutrition section | The user enters values | Fields are labelled with units (kcal, g, mg) and are all optional; a blank field is stored as `null` (unknown), never `0` | Unit + BDD + E2E |
| AC-31 | Nutrition values | Saved with any negative value, energy over 900 kcal, sodium over 40,000 mg, any gram value over 100, saturated fat greater than fat, or sugar greater than carbohydrate | `400` Problem Details identifying each failing field | Unit + BDD |
| AC-32 | Any nutrition value changes | Saved | `NutritionUpdatedAtUtc` is set to the server time; it is unchanged if no nutrition value changed. The user may edit `NutritionSource` (free text, e.g. "woolworths", "manual") | Unit + BDD |
| AC-33 | The "Exclude from grocery list" checkbox | Toggled and saved | `ExcludeFromGroceryList` is persisted and shown on reload | BDD + E2E |
| AC-34 | Ingredient X loaded at version V, then changed elsewhere | X is saved with version V | `409` Problem Details; the form keeps the typed values and shows "This ingredient was changed elsewhere" with a "Reload latest" action | BDD + E2E |
| AC-35 | The user has unsaved changes | They navigate away from the form | They are asked to confirm discarding changes | E2E |

### Archive & restore

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-36 | An active ingredient | The user selects Archive and confirms | `ArchivedAtUtc` is set, it disappears from the default list, and the archive endpoint returns `204` | BDD + E2E |
| AC-37 | An archived ingredient | The user selects Restore | `ArchivedAtUtc` is cleared and it reappears in the default list | BDD + E2E |
| AC-38 | An archived ingredient whose name now matches an active ingredient | Restore is attempted | `409` Problem Details; it stays archived | BDD |
| AC-39 | Any ingredient | A client sends `DELETE` | No delete endpoint exists (`405`); there is no hard delete in this PRD | BDD |

### Categories, departments & supermarkets

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-40 | A user | They call `GET /api/categories` or `GET /api/departments` | Their records are returned sorted by name | BDD |
| AC-41 | A name that already exists for the owner (case-insensitive, whitespace-normalised) | `POST /api/categories` or `/api/departments` is called | `200 OK` with the **existing** record (idempotent create), not a duplicate | BDD |
| AC-42 | A blank name or one over 100 characters | Created | `400` Problem Details | BDD |
| AC-43 | Any authenticated user | They call `GET /api/supermarkets` | Seeded chains (Woolworths, New World, Pak'n Save), each with their stores (code, display name, display order), are returned | BDD |

### Standalone Firestore migration — dry-run and classification

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-44 | The standalone tool | It runs without an explicit write option | It performs a dry-run only; no database writes occur, and no in-app import route or endpoint is introduced | Unit + BDD |
| AC-45 | A backup directory and target owner | A run starts | The tool reads `ingredients.json`, `meta.json` and the migration decision file; it verifies the target database schema is current and the owner already exists, and displays the target database identity (without credentials) and owner before writing | BDD |
| AC-46 | A valid export | Previewed | The summary counts new / existing categories and departments, new / unchanged / changed ingredients, unclassified invalid items, mapped / not-stocked / unmapped store relationships, links and nutrition; changed ingredients include complete old-to-incoming diffs | Unit + BDD |
| AC-47 | An ingredient whose `id` is missing, duplicated or not a valid GUID | Previewed | It is listed as a **blocking error**; no replacement GUID is generated | Unit + BDD |
| AC-48 | An ingredient whose ID already exists under **another** owner | Previewed | A generic ownership conflict blocks the run without exposing the other owner's data | BDD |
| AC-49 | An ingredient ID already exists for the target owner | Previewed | Source-controlled fields are compared; identical values are unchanged, otherwise it is proposed for update, including additions, removals and cleared values in child collections | Unit + BDD |
| AC-50 | Names collide after normalisation in the proposed final state, including archived ingredients | Previewed | A **blocking error** identifies the conflict; distinct ingredient IDs are never merged automatically | Unit + BDD |
| AC-51 | A department key is not a seeded store code, or a product-link key is not a seeded chain code | Previewed | A **blocking error** names the unknown key | Unit + BDD |
| AC-52 | A category or department value is not in `meta.json` | Previewed | A **warning**; the value is proposed for creation and existing normalised names are reused | Unit + BDD |
| AC-53 | A department value of `""` / a missing store key | Previewed | It is counted as unmapped (no mapping created) | Unit |
| AC-54 | A department value of `n/a` (any case) | Previewed | It will be imported as a not-stocked mapping | Unit |
| AC-55 | Representable nutrition breaks the AC-31 business rules (e.g. saturated fat > fat) | Previewed | A **warning** names the ingredient and rule; approved data keeps the source values. Malformed numbers or values outside the database representation block the run | Unit + BDD |
| AC-56 | Fields the import does not map (e.g. `nutrition.manuallyEdited`, or any unknown key) | Previewed | They are listed as **ignored fields** with counts | Unit + BDD |
| AC-57 | A `preferredVariant` that matches no `variantLabel` on that ingredient's links | Previewed | A **warning**; no link is marked preferred | Unit |
| AC-58 | An `invalid: true` item has no valid recorded classification | Previewed | Its GUID and name are listed for an explicit `Ingredient`, `Heading` or `Note` decision; suggestions are advisory only. A write run is blocked until every such item is classified | Unit + BDD |
| AC-59 | A required file is missing, malformed or has an unsupported shape, or a decision is duplicated or uses an unsupported value | Read | A clear diagnostic and nonzero exit code are returned; nothing is written | Unit + BDD |

### Standalone Firestore migration — approved writes and repeat runs

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-60 | An explicit write run has no blocking errors | Changes are applied | New ingredients are added without per-item approval; each changed ingredient is prompted individually after its diff is shown. All additions and accepted updates are written in one transaction | BDD |
| AC-61 | Source files, classifications or relevant target data change after review | Writes are attempted | The reviewed plan is rejected with a nonzero exit code and no writes; the user must run and review again | BDD |
| AC-62 | An update is declined or has no explicit acceptance | The run completes | That ingredient and its mappings and links remain unchanged; new ingredients and other accepted updates can still be committed | BDD |
| AC-63 | A write or concurrency failure occurs | Applying changes | All database writes roll back; an explicit error is returned | BDD |
| AC-64 | The operator cancels or interrupts before commit | The run stops | Nothing is written; no database staging state remains | BDD |
| AC-65 | A successful import | The same backup is rerun without intervening target changes | Previously accepted records are unchanged, with no duplicate entities or links; previously declined differences remain visible for review | BDD |
| AC-66 | A new or accepted changed ingredient | Inspected via the API | Its preserved GUID and mapped source values match the backup, including nutrition source timestamp, link order, variant label, note and pricing flag; a uniquely matched preferred variant marks its link preferred | BDD |
| AC-67 | An item is classified as `Heading` or `Note` | Imported | No Ingredient is created; the decision is retained for Recipe migration. If an Ingredient already exists for that ID, the run blocks for explicit resolution rather than deleting it | BDD |
| AC-68 | An item is classified as `Ingredient` | Imported | It is created or proposed for update with `ExcludeFromGroceryList = true` | BDD |
| AC-69 | A target ingredient is absent from a newer backup | Imported | It is not deleted or archived; existing archive status and creation timestamp are preserved for all updates | BDD |
| AC-70 | A run finishes | Its summary is printed | Counts distinguish inserted, updated, unchanged, declined, headings and notes skipped, warnings and errors; declined updates are not reported as synchronised | BDD |
| AC-71 | A persistent local target database with an existing registered owner | EF migrations and the tool run | Schema is created or upgraded through EF migrations, the backup imports successfully, and records remain available after restarting PostgreSQL | BDD |

## Technical Requirements & Data Schema

### Persistence

- Rename `AuthDbContext` to `AppDbContext`. Move each entity's mapping (including the existing `User`, `AuthSession` and `PasswordResetToken`) into its own `IEntityTypeConfiguration<T>` class applied with `ApplyConfigurationsFromAssembly`. Update the `[DbContext]` attribute on existing migrations and rename the snapshot. **The rename must not produce any schema change:** a generated migration after the rename must be empty and is discarded.
- Then add one migration per slice for the new schema and seed data.

### Database readiness and deployment boundary

- The initial import target is the persistent local PostgreSQL database already defined in `compose.yaml`, not an ephemeral component-test database. PRD03 includes documenting its setup, applying EF migrations and rehearsing the import into it.
- Register the destination account through the existing authentication flow before importing; Firestore user IDs are not destination account IDs. The tool requires an explicit target owner ID and database connection, with credentials supplied through User Secrets or environment variables, never command-line arguments or committed files.
- Schema changes use EF migrations. The tool only reads and writes application data through EF Core; it neither provisions databases nor silently applies migrations.
- No production database exists yet. Import does not require one. Production database and API/web hosting, restricted connectivity, secret injection, backup/restore verification and deployment procedures must be delivered before production cutover in separate deployment work. If PRD03 is required to launch remotely, that work becomes an explicit prerequisite rather than an implicit import feature.

### Entities

| Entity | Columns | Constraints |
|---|---|---|
| `SupermarketChain` | `Id` (Guid, seeded), `Code`, `Name` | `Code` unique. Seed: `woolworths` / Woolworths, `new-world` / New World, `pak-n-save` / Pak'n Save |
| `Store` | `Id` (Guid, seeded), `ChainId`, `Code`, `Name`, `DisplayOrder` | `Code` unique. Seed: `new-world-greenmeadows` / New World Greenmeadows / 1; `pak-n-save-tamatea` / Pak'n Save Tamatea / 2; `woolworths-carlyle` / Woolworths Carlyle / 3 |
| `Category` | `Id`, `OwnerUserId`, `Name`, `NormalizedName` | Unique (`OwnerUserId`, `NormalizedName`); FK to `Users` |
| `Department` | `Id`, `OwnerUserId`, `Name`, `NormalizedName` | Same as `Category` |
| `Ingredient` | `Id`, `OwnerUserId`, `Name`, `NormalizedName`, `CategoryId?`, `ExcludeFromGroceryList`, nutrition (below), `NutritionSource?`, `NutritionUpdatedAtUtc?`, `CreatedAtUtc`, `UpdatedAtUtc`, `ArchivedAtUtc?`, `xmin` | Unique (`OwnerUserId`, `NormalizedName`) across active **and** archived; index on (`OwnerUserId`, `ArchivedAtUtc`, `NormalizedName`) for the list query. `Id` is not database-generated: the app supplies a Guid v7 on create, or the import supplies the Firestore GUID |
| `IngredientStoreMapping` | `IngredientId`, `StoreId`, `DepartmentId?`, `IsNotStocked` | PK (`IngredientId`, `StoreId`); check constraint: exactly one of `DepartmentId` set or `IsNotStocked` true; cascade delete with the ingredient |
| `ProductLink` | `Id`, `IngredientId`, `ChainId`, `Url`, `VariantLabel?`, `Note?`, `SupportsBothPricingMethods`, `IsPreferred`, `Position` | Unique (`IngredientId`, `ChainId`, `Position`); filtered unique index: at most one `IsPreferred` per (`IngredientId`, `ChainId`); `Url` max 2,048 |

**Nutrition** is an owned type `NutritionPer100g` mapped to columns on `Ingredient`: `EnergyKcal`, `ProteinG`, `CarbohydrateG`, `SugarG`, `FatG`, `SaturatedFatG`, `FibreG`, `SodiumMg`. All are `numeric(9,3)`, nullable, and independently optional.

**Name normalisation:** trim, collapse internal whitespace to one space, then lower-case using the invariant culture to produce `NormalizedName`. The display `Name` keeps the user's casing. Imported names are kept exactly as in Firestore (all lowercase); title-casing for display is deferred.

### Ownership & current user

A scoped `ICurrentUser` service reads the user ID from the existing `ClaimTypes.NameIdentifier` claim. Repositories filter every query by `OwnerUserId` explicitly. EF global query filters are not used, because the context is created through `AddDbContextFactory` and a filter would depend on a scoped service. Cross-owner access returns `404`.

### API

All endpoints require authentication, accept and return DTOs (never entities), pass `CancellationToken` through, and return RFC 7807 Problem Details on error via the existing `GlobalExceptionHandler`.

| Method | Route | Purpose | Success |
|---|---|---|---|
| `GET` | `/api/ingredients?search=&page=1&pageSize=25&archived=false` | Paged summary list: id, name, category name, `excludeFromGroceryList`, `archived`, and per-store state (`mapped` + department name / `notStocked` / `unmapped`). Includes `totalCount` | 200 |
| `GET` | `/api/ingredients/{id}` | Full detail including mappings, product links, nutrition and `version` | 200 |
| `POST` | `/api/ingredients` | Create | 201 + `Location` |
| `PUT` | `/api/ingredients/{id}` | Full replace of editable fields; requires `version` | 200 |
| `POST` | `/api/ingredients/{id}/archive` | Archive (idempotent) | 204 |
| `POST` | `/api/ingredients/{id}/restore` | Restore (idempotent) | 204 |
| `GET` / `POST` | `/api/categories` | List / idempotent create | 200 |
| `GET` / `POST` | `/api/departments` | List / idempotent create | 200 |
| `GET` | `/api/supermarkets` | Chains with nested stores | 200 |

Archive and restore are state transitions, not deletions, so they are explicit actions rather than `DELETE`.

### Web

- Routes: `/ingredient-maintenance` (list), `/ingredient-maintenance/new`, `/ingredient-maintenance/{id}`. PRD 02's longest-prefix matching keeps Ingredient Maintenance active on all of them. Page titles: "Ingredient Maintenance", "Add Ingredient", "Edit Ingredient". There is no import page.
- One typed API service (`ingredientsApi.ts`), with Zod schemas validating every response at the boundary.
- Data fetching and side effects live in hooks (`useIngredientList`, `useIngredient`, `useSupermarkets`, `useLookupList` for categories and departments). Components are presentation-only.
- Small single-purpose components, each under 200–250 lines: list page, search box, pagination, desktop table, mobile card, mapping-state cell, ingredient form split into sections (details, store mappings, product links, nutrition), a reusable type-ahead with "Create" option, and archive confirmation.
- Breakpoint: reuse the 700px `MOBILE_BREAKPOINT` from `navigation.ts`.
- Styles: new modular files under `apps/web/src/styles/`: `table.css`, `cards.css`, `forms.css`, `badges.css`. All colours, including the "Missing" warning and "Not stocked" muted state, are variables in `colors.css`.

### Firestore migration (standalone temporary tool)

**Isolation:** a small .NET console project under `tools/FirestoreMigration/` reuses `AppDbContext` and its entity configurations. Its parser, comparison and orchestration code stay outside the API and web app. The application never references the tool. No controllers, uploads, feature flags or staging tables are needed. Recipe and Grocery List migration may extend the tool later, but are not implemented in PRD03.

**Inputs and execution:**
- Explicit backup directory, target owner ID and migration decision file; database credentials come from secret configuration. Read only `ingredients.json` and `meta.json`, not unrelated user or recipe exports.
- Default execution is read-only: print a summary, blocking errors, warnings, ignored fields, unclassified invalid items and complete per-ingredient diffs.
- An explicit write option starts a fresh preview, prompts for each changed ingredient and commits additions and accepted updates in one transaction. New ingredients require no individual approval. There is no unattended "accept all updates" mode in PRD03. No response means no approval; interruption cancels the run before commit.
- Print the database identity and destination owner, without credentials, so the operator can verify the target. Errors are explicit diagnostics with nonzero exit codes, not HTTP responses.

**Repeat-import contract:**
- Match ingredients by GUID. Compare mapped values semantically, not raw JSON, generated child IDs, target audit timestamps or whitespace/casing of lookup references. Resolve categories and departments by normalised name. Reuse unchanged link rows where possible.
- Each accepted update replaces the ingredient's source-controlled name, category, store mappings, product links, nutrition values and source metadata. The diff must include removed links, missing/cleared source fields, and mapping transitions, not just additions. Preserve `Id`, `OwnerUserId`, `CreatedAtUtc` and `ArchivedAtUtc`; update `UpdatedAtUtc` when a change is applied. Preserve the source nutrition timestamp rather than replacing it with the import time.
- Category and department lists are additive: reuse existing normalised names and create missing values; never infer renames, merges or deletions from the name-only lists.
- Non-invalid source ingredients import with `ExcludeFromGroceryList = false`; invalid items classified as `Ingredient` use `true`. Changes to this value on existing rows appear in the approval diff.
- Existing IDs owned by another user, duplicate IDs, unique-name collisions and incompatible classification changes block the run. Declining an ingredient update also declines its child changes. Create lookup rows only for master-list entries and applied ingredients, not values found solely in declined updates.
- A target record absent from the backup is untouched. The tool has no previous-import snapshot and cannot infer which application's edit is newer. Approval explicitly chooses the incoming values; it is not a three-way merge or continuous synchronisation service.
- Capture content fingerprints for both source files and the decision file, and versions of relevant target rows while preparing the plan. Before committing, verify the inputs and database read-set still match the reviewed state, including mappings and links. Use a transaction with appropriate conflict detection to prevent concurrent changes or inserts from bypassing this check; any conflict rolls back the entire run and requires review again. A file hash alone cannot protect against target changes.

**Classification decisions:**
- Retain a small operator-reviewed JSON decision file keyed by legacy ingredient GUID, recording `Ingredient`, `Heading` or `Note`, with a readable legacy name for review. Names are informational; GUIDs identify decisions.
- Suggestions (such as a trailing colon suggesting a heading) never become accepted decisions automatically. List all unclassified invalid items and block writes until the operator records each choice.
- Retain decisions across backups, including IDs no longer present, for future Recipe migration; report absent entries rather than rejecting them. A decision for a currently non-invalid ingredient or a changed classification incompatible with an existing Ingredient blocks for explicit resolution.
- Only `Ingredient` decisions create excluded ingredients. `Heading` and `Note` decisions create no database rows. The future Recipe importer consumes this same file; an unresolved ID is a migration error, never an inferred heading.
- Keep the actual decision file and real backups outside the repository and backed up together through Recipe and Grocery List migration. Document the format with a synthetic example; commit no personal backup data.

**Source mapping:**

| Firestore | New model |
|---|---|
| `ingredients[].id` | `Ingredient.Id` (must parse as GUID) |
| `ingredients[].name` | `Name` (unchanged) |
| `ingredients[].category` | `CategoryId`, matched by normalised name; absent → `null` |
| `ingredients[].departments.{storeCode}` | `IngredientStoreMapping`: non-empty → department matched by normalised name; `n/a` (any case) → `IsNotStocked`; `""` or missing key → no row |
| `ingredients[].productUrls.{chainCode}[i]` | `ProductLink` with `Position = i`; `url`, `variantLabel`, `note`, `supportsBothPricingMethods` (absent → `false`) |
| `ingredients[].preferredVariant` | `IsPreferred = true` on the link whose `variantLabel` matches |
| `ingredients[].invalid` | Per the retained `Ingredient` / `Heading` / `Note` decision (D4); absent → `false`, imported normally |
| `nutrition.per100g.calories, protein, carbohydrates, sugar, fat, saturatedFat, fiber, sodium` | `EnergyKcal`, `ProteinG`, `CarbohydrateG`, `SugarG`, `FatG`, `SaturatedFatG`, `FibreG`, `SodiumMg` |
| `nutrition.source` | `NutritionSource` |
| `nutrition.lastUpdated` (`_seconds`, `_nanoseconds`) | `NutritionUpdatedAtUtc` |
| `nutrition.manuallyEdited` | Ignored (reported) |
| `meta.json` `categories.list`, `departments.list` | `Category`, `Department` rows; existing names (normalised) reused |
| `meta.json` `supermarkets` | Validated against the seeded store codes; not imported |

**Removal checklist** (after Recipe and Grocery List migration and final verification are complete): remove the tool project, its solution entry and tests, and its secret configuration. Retain source backups, decisions and final migration summaries according to the agreed backup policy. No application routes, registrations or schema changes need removal.

**Rehearsal and cutover:** while the old app remains active, import newer backups and review updates as needed. Before the eventual production cutover, stop edits in the old app, take a final backup, run the tool against the migrated production schema with the correct owner, resolve all declined updates and errors, and verify counts, relationships and representative values before switching usage. Rehearse this procedure locally first. Recipe and Grocery List verification belongs to their migration PRDs.

**Source files:** examples and counts in this PRD describe the reviewed October backup, not a fixed import dataset. The tool must accept later backups and report new anomalies. Test fixtures use small synthetic samples, never the real export. Document parameter meanings and dry-run/write behaviour before runnable command examples.

## Future Recipe Considerations (guidance only — not implemented)

- Recipe ingredient lists should be modelled as ordered **recipe items** with a `Type` of `Ingredient`, `Heading` or `Note`. Only `Ingredient` items carry an `IngredientId`. "Serving suggestions:", "Optional:" and "Setup" become `Heading` items, so no fake ingredients are needed.
- The Recipe import will reference ingredients by the preserved Firestore GUIDs and consume the retained decision file to create `Heading` or `Note` items explicitly. A missing Ingredient without such a decision is an unresolved-reference error, never a heading by inference. The reviewed backup has 37 completely blank recipe lines (no ID or name), which should be dropped.
- Future Recipe migration should reuse the new / unchanged / changed comparison and explicit-update-approval approach. Recipe source IDs are not GUIDs; choose their identity strategy in the Recipe PRD rather than applying the Ingredient GUID rule to them.
- Recipe lines in Firestore also contain a copy of the ingredient name (often in different casing) and sometimes a `department`. These are stale copies and should be ignored in favour of the Ingredient record.
- Archived ingredients remain valid references for existing recipes but should be hidden from pickers when adding new recipe items.
- Grocery list items in Firestore (`users/{id}/meta`) also reference ingredient IDs, so they depend on D3.

## Future Nutrition Considerations (guidance only — not implemented)

- Per-100 g values alone cannot calculate "2 eggs" or "1 cup flour". Recipe nutrition will need per-ingredient unit conversions (grams per piece, density for volume units). This is an additive change to the Ingredient model.
- **Known data-quality limitation:** the old app appears to have stored `0` where a value was unknown (104 fibre and 70 saturated fat values are `0`). These cannot be told apart from true zeros and are imported as `0`. Future calculations should treat this as a source of under-estimation.
- Three source records break the nutrition rules (chopped walnuts and soy milk have saturated fat greater than fat; sugar's protein, fat and carbohydrate total 102 g). They import with warnings and must be corrected before they can next be saved. A "total macros ≤ 100 g" rule was deliberately **not** adopted because rounding produces legitimate violations.

## Edge Cases & Failure Scenarios

- **Concurrent edits** from two devices on the shared account: `409` with the user's input preserved (AC-34).
- **Archived name collision** on create or restore: `409` with guidance to restore instead (AC-20, AC-38).
- **Non-food items:** the source contains items in categories such as Electronics, Kitchenware and Baby (5 items). They are imported as ingredients with no nutrition. The Grocery List PRD should decide whether a separate grocery item concept is needed.
- **Stores with no mapping:** 19 source ingredients have no store keys and 30 have only some. These show as "Missing" (AC-09), which is the purpose of the desktop table.
- **Unused master-list values** (e.g. department "aisle 13") are still imported so they are available to choose.
- **Mixed-case master lists** ("Baby" vs "baking aisle") are imported unchanged; uniqueness is case-insensitive.
- **Network or API failure** on load, save or archive: an error is shown (`role="alert"`) with a retry option; the form keeps the user's input. Migration connection and write failures produce explicit diagnostics and roll back writes.
- **Session expiry** mid-edit: the existing PRD 02 behaviour applies (redirect to login, then return to the page). Unsaved form input is lost; this is accepted.
- **Import re-run:** new IDs are added; unchanged data is skipped; differences require individual approval. Declined differences remain visible on subsequent runs (AC-60 through AC-70).
- **Changing backups or concurrent maintenance:** reject stale reviewed plans rather than applying unreviewed changes (AC-61).
- **No production environment yet:** use persistent local PostgreSQL for rehearsal; deployment and verified backups are prerequisites for production cutover, not assumptions made by the tool.
- **Offline use while shopping:** not supported in this PRD; a failed save shows the error and keeps the input.

## Implementation Slices

Each slice is one story and one PR, implemented test-first.

1. **Persistence foundation:** rename to `AppDbContext` with per-entity configurations (empty-diff migration check); add `ICurrentUser`; add the supermarket, store, category and department schema and seed; `GET /api/supermarkets`, categories and departments endpoints. Document persistent local database readiness and the production deployment boundary.
2. **Ingredient schema and read path:** Ingredient, mapping and product-link schema; list and detail endpoints; list page with desktop table, mobile cards, search, paging and the archived toggle.
3. **Standalone Firestore migration tool:** dry-run, retained Ingredient/Heading/Note decisions, complete diffs, per-ingredient update approval, transactional writes and repeat-import tests against PostgreSQL. Document operation and rehearsal/cutover steps. Comes before create/edit so real data is available to validate the table; no import API or page.
4. **Create and edit:** form, validation, type-ahead create, concurrency handling.
5. **Archive and restore.**

## Test Strategy

- **Unit (xUnit + FluentAssertions, Moq/NSubstitute):** name normalisation; nutrition validation rules; mapping-state invariants; product-link rules. Separate tool tests cover parsing, mapping, semantic comparison and complete diffs, classification decisions, approval handling, timestamp conversion and ignored-field detection, using small synthetic JSON fixtures.
- **Unit (Vitest):** Zod schemas, mapping-state cell rendering, type-ahead create behaviour, form validation messages.
- **BDD (Reqnroll):** application criteria marked BDD run against the API with an isolated PostgreSQL test database. Feature files: `Ingredients/Ownership.feature`, `Ingredients/IngredientList.feature`, `Ingredients/IngredientEditing.feature`, `Ingredients/Archiving.feature`, `Ingredients/Lookups.feature`. Tool criteria marked BDD exercise its command workflow directly against isolated PostgreSQL, with controlled operator input; no import HTTP endpoints are introduced. Keep tool scenarios separate for removal. Prove rollback, stale-plan rejection (including child-only changes), update decline, unchanged reruns, preserved IDs and restart persistence; dry-run unit tests alone are insufficient.
- **E2E (Playwright):** every criterion marked E2E, on the desktop and mobile viewport projects, with the API mocked in the style of `tests/e2e/support/mockAuthApi.ts`. Uses `getByRole` and `data-testid`; no arbitrary timeouts. No browser tests are needed for the migration tool.
- **JMeter:** not applicable (single-account app; about 300 ingredients).

Tests are written **before** implementation (TDD).
