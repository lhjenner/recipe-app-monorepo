# PRD 05: Grocery List & Purchase History

## Status & Traceability

- Status: Draft; product decisions confirmed during planning, with remaining recommendations identified below.
- Epic: Not yet created.
- Implementation issues and pull requests: Not yet created. Section 14 defines proposed slices, not existing GitHub work.
- Depends on: [PRD01 - Authentication & Login](./01-authentication-and-login.md), [PRD02 - App Shell & Navigation](./02-app-shell-and-navigation.md), [PRD03 - Ingredient Maintenance](./03-ingredient-maintenance.md), and [PRD04 - Recipe Foundation & Browsing](./04-recipe-foundation-and-browsing.md).
- Each implementation issue and PR must link this PRD and identify its applicable acceptance criteria.
- This document does not authorize production provisioning, legacy-data writes, changes to the old application, or implementation before the draft decisions are reviewed.

## 1. Overview & Problem Statement

Users need one ongoing grocery list for both planned shops and occasional supermarket visits. They add ingredients from existing Recipes or enter items manually, sometimes buying only part of what is needed. A weekly-list lifecycle would force users to define artificial shopping boundaries.

The legacy app supports this ongoing workflow, but stores the list as a document-wide array, separates compatible units into different lines, and conflates recipe requirements with purchase quantities. Users also delete bought items to keep the screen clear, losing information that could later help compare intended purchases with supermarket orders or deliveries.

PRD05 introduces a durable, user-owned Grocery List with compatible-unit consolidation and purchase-event history. Purchased items leave the default outstanding view automatically; undo restores the purchased quantity without erasing history. Saving a manual item as a permanent Ingredient remains an explicit, optional action, consistently supported for single and batch entry.

This is not Recipe authoring, nutritional calculation, a supermarket checkout system, or a delivery-verification feature. The locally running API may write to persistent local PostgreSQL during development; a hosted production database is not a prerequisite for implementing this feature.

## 2. Scope, Goals & Non-Goals

### In scope

- One ongoing Grocery List per authenticated owner, initially empty.
- Add an active existing Recipe's eligible Ingredient items, retaining source contributions.
- Confirm intentional repeat addition while that Recipe still contributes outstanding items.
- Add and edit manual items, with or without a linked Ingredient.
- Optional save-as-Ingredient per manual item, consistently for single and batch entry.
- Prompted master-Ingredient department updates for the currently selected supermarket when editing a Grocery Item linked by Ingredient ID.
- Consolidate compatible requirements using NZ measures: teaspoon 5 ml, tablespoon 15 ml, cup 250 ml.
- Separate recipe requirements from an explicit purchase quantity and purchase unit.
- Store-based department grouping, including unmapped and not-stocked states.
- Full and partial purchases, automatic hiding, completed-item visibility, date-filtered history and undo.
- Explicit removal/cancellation without falsely recording a purchase.
- Ownership, optimistic concurrency, atomic writes, safe command retries, and accessible responsive UI.
- Automatic polling while the Grocery List is visible, with change notices and explicit stale/offline states.
- An initially empty, persistent list without importing legacy Grocery List data; explicit database-authority/cutover guidance.

### Goals

1. Make the outstanding list useful on a phone during a supermarket visit.
2. Preserve purchase history without requiring named lists, weekly closure or checkout sessions.
3. Consolidate safe quantities without inventing density, pack sizes or meanings for ambiguous text.
4. Keep ad-hoc shopping items separate from the reusable Ingredient catalogue unless the user chooses otherwise.
5. Prevent lost updates and duplicate purchases/additions during concurrent use or uncertain network outcomes.
6. Preserve enough source and event information for later nutrition and online-cart features.

### Non-goals

- Recipe creation, editing, duplication, archive/restore controls or image authoring.
- Common Items, Pending Items, their migration, or any changes to their legacy behavior.
- Woolworths extension adaptation, cart automation, stock checks, product-price comparison or website access.
- Online orders, payments, delivery reconciliation, substitutions, receipts, costs or purchase-session management.
- Multiple named grocery lists, weekly list archives, account sharing beyond existing authentication, or public lists.
- Food Diary, calories, nutrient aggregation, ingredient density tables, piece weights or ingredient-specific volume-to-mass conversion.
- Automatic pack-size selection, grams-to-pack calculation, recipe scaling or serving adjustment.
- Meal Planner integration, offline writes, live push synchronization or a new realtime provider.
- Automatic reconciliation of simultaneous legacy and new-app edits, or treating Firestore as a PostgreSQL restore source.
- Legacy Grocery List import, including legacy completed-item state and purchase history.
- Production database/application provisioning or changes to selected storage providers.

## 3. Current Implementation & Legacy Evidence

Repository paths below reflect the current feature-folder layout, rather than older paths retained in previous PRDs.

| Surface | Confirmed behavior / implication |
|---|---|
| [Application routes](../../apps/web/src/app/App.tsx) and [navigation](../../apps/web/src/app/navigation.ts) | `/grocery-list` is an authenticated stub. Replace it and add history routing without changing menu order or making other stubs functional. Reuse the 700 px breakpoint. |
| [Recipe aggregate](../../apps/api/src/RecipeApp.Api/Infrastructure/Persistence/Recipe.cs) and [RecipeStepItem](../../apps/api/src/RecipeApp.Api/Infrastructure/Persistence/RecipeStepItem.cs) | Recipes, Steps and items have stable GUIDs. Items distinguish Ingredient, Heading and Note. Quantities and units remain text; interpretation must not rewrite them. |
| [Ingredient write service](../../apps/api/src/RecipeApp.Api/Ingredients/IngredientWriteService.cs) and [write requests](../../apps/api/src/RecipeApp.Api/Ingredients/IngredientWriteRequests.cs) | Existing validation, owner checks and concurrency must be reused. The current service owns its database context; combined Grocery/Ingredient writes require a shared transaction boundary, not sequential HTTP requests. |
| [Shared title-case helper](../../apps/web/src/shared/utils/titleCase.ts) and [LookupTypeahead](../../apps/web/src/features/ingredients/components/LookupTypeahead.tsx) | Reuse these for names and Ingredient lookup. Extract genuinely shared placement if needed; do not duplicate casing or introduce a different dropdown. |
| [Ingredient migration contract](./03-ingredient-maintenance.md#firestore-migration-standalone-temporary-tool) and [Recipe repeat-import contract](./04-recipe-foundation-and-browsing.md#apply-and-repeat-behavior) | Changed matched records require approval; absent destination records are retained. Imports are not three-way merges. App-authored data requires a deliberate authority/cutover policy. |

The legacy repository at `C:\projects\recipe-catalog` was inspected read-only during planning. No exports, credentials or real user data are copied into this document. Source paths are evidence locations, not dependencies of the replacement app:

| Legacy source path | Confirmed behavior |
|---|---|
| `src/pages/grocery-list/hooks/useGroceryData.ts` | One `users/{uid}/meta/groceryList` document contains an items array; realtime subscription; several edits/removals overwrite the full array. |
| `src/pages/grocery-list/hooks/useModalManagement.ts` and `src/components/ingredient-edit-modal/hooks/utils/ingredientFormUtils.ts` | Main manual-entry flow defaults catalogue saving off; single entry can create an Ingredient. Editing a linked item deliberately updates its master Ingredient. PRD05 preserves prompted department updates, limited to the currently selected supermarket. |
| `src/components/ingredient-edit-modal/hooks/useMultiIngredientSubmission.ts` | Legacy batch entry disables catalogue saving; inconsistent with single entry. PRD05 replaces this limitation with independently selectable catalogue saving and expandable Category/Departments fields per row. |
| `src/pages/recipe/utils/groceryListHelpers.ts` and `src/pages/grocery-list/utils/groceryMerging.ts` | Recipe requirements are parsed, filtered and merged; matching includes the exact unit. Name patterns and legacy `invalid` flags exclude items. Countable/continuous heuristics influence purchase quantity. |
| `src/pages/grocery-list/types/grocery.ts` | Item `id` is an optional Ingredient ID, not a reliable unique grocery-line ID. Sources reference legacy Recipe IDs. |
| `extension/inject.js` and `functions/src/api/markItemsPurchased.ts` | Cart success is reported by ingredient name and marks matching items purchased; no replacement extension work is implied by this PRD. |
| `tests/e2e/features/groceryList.feature` | Mostly commented scenario notes; not evidence of active behavioral coverage. |

PRD05 preserves agreed workflows, not legacy implementation shortcuts. Legacy current-list, Common/Pending or shopping-history data has not been inventoried for migration.

## 4. Product Decisions

### Confirmed during planning

| ID | Decision | Rationale |
|---|---|---|
| D1 | One ongoing list with purchase-event history from the first release. | Shopping is not reliably weekly or completed in one trip. |
| D2 | Purchased items hide automatically; expose completed items, date-filterable history and undo. | Keep the screen clear without deleting evidence of purchases. No midnight reset. |
| D3 | Buy the full outstanding purchase quantity by default; allow a smaller quantity. | Support partially available stock and intermittent shopping. |
| D4 | Manual entry always adds to the Grocery List; catalogue saving defaults off and is explicit per item in both single and batch entry. | Ad-hoc items must not silently become Ingredients. |
| D5 | Apply NZ teaspoon/tablespoon/cup measures and consolidate compatible units. | Resolve the agreed spoon/cup duplication problem without needing ingredient densities. |
| D6 | Ask before adding another Recipe's worth while the same Recipe contributes outstanding items. | Distinguish intentional repetition from an accidental click. |
| D7 | Core Grocery List and optional Ingredient saving are in scope; Common Items, Pending Items and the extension are deferred. | Keep delivery focused while defining stable contracts for later work. |
| D8 | In the future extension, confirmed cart addition counts as purchased in app behavior, with its event origin recorded as OnlineCart. | Preserve the user's chosen behavior without presenting cart success as evidence of payment or delivery. PRD05 creates only manual-purchase events. |
| D9 | Future Pending selection promotes items before cart attempts; failures remain outstanding, not Pending. | Selection expresses a decision to buy soon independently of availability. No Pending implementation in PRD05. |
| D10 | When editing a Grocery Item linked by Ingredient ID, the only editable master-Ingredient field is its department for the supermarket currently selected on the Grocery List. Prompt before saving that change to the same Ingredient. Shopping-only edits do not prompt or update the catalogue. | Correct the current shop's department without exposing Ingredient name, category, other stores or other catalogue fields for editing. |

### Reviewed recommendations

| ID | Recommendation | Boundary |
|---|---|---|
| P1 | Consolidate by linked Ingredient identity plus compatible dimension; unlinked items use normalized name plus dimension. Keep count/container labels distinct. | Do not silently link ad-hoc names to Ingredients or equate a tin with a piece. |
| P2 | Measured requirements suggest one pack/item to buy, explicitly labelled as a suggestion requiring confirmation; count requirements suggest their rounded-up count. | No guessed pack contents or automatic weight purchase. Persist the confirmed purchase unit separately from requirements. |
| P3 | In batch entry, up to 10 items; names follow the existing 200-character rule; editable shopping notes allow up to 250 characters. Editable purchase quantities must be greater than zero and at most 9,999, regardless of Ingredient or purchase unit; measured quantities allow decimals. | Apply the same input limit in API and UI. This does not cap Recipe requirement totals, truncate consolidated/restored demand or rewrite existing Recipe notes. |
| P4 | Display timestamps and interpret history/completed dates, including "today", in New Zealand time (`Pacific/Auckland`), regardless of browser timezone. Completed/history default to today with an explicit all-dates option. | Store UTC instants internally; derive UTC query boundaries from Auckland calendar dates using daylight-saving rules. Dates affect visibility, never retention or undo eligibility. |
| P5 | The first visit shows an empty list; no legacy Grocery List import is needed. Added items, purchases and history persist across navigation, reloads and API/database restarts. | Empty startup is not a reset-on-navigation policy. Never overwrite new-app data with a legacy export. |
| P6 | Automatically poll every 15 seconds while the Grocery List page is visible. Pause when hidden/backgrounded or the phone is locked; fetch immediately on returning, then resume. Mutation responses refresh affected views. | No user refresh is required to discover changes. Preserve dirty forms, announce changes and expose failed checks/staleness. Browsers may suspend execution; no guarantee of locked/background polling or delivery within 15 seconds plus network time. |

P1-P6 are accepted. Consolidation and Undo may produce outstanding demand above 9,999; preserve it exactly rather than rejecting or truncating those operations. Display such totals with an explanation of the editable input limit. The limit applies to user-entered quantities, not the Buy all action that records an existing outstanding balance.

## 5. User Stories

- As a user, I want one ongoing grocery list so that I can shop whenever necessary without closing a weekly list.
- As a user, I want to add a Recipe's eligible ingredients so that I do not have to enter them individually.
- As a user, I want compatible amounts of the same Ingredient consolidated so that teaspoons and tablespoons do not become duplicate shopping lines.
- As a user, I want recipe requirements and purchase quantities shown separately so that I can decide how many packs or how much produce to buy.
- As a user, I want to add ad-hoc items without creating Ingredients so that occasional purchases do not clutter my catalogue.
- As a user, I want to save selected single or batch items as Ingredients so that I can reuse them later.
- As a user, I want to correct a linked Ingredient's department for the currently selected supermarket from the Grocery List with confirmation so that future shops use the corrected department.
- As a user, I want store-based grouping so that I can find items efficiently while shopping.
- As a user, I want bought items to disappear from the outstanding view so that I can concentrate on what remains.
- As a user, I want to record a partial purchase so that unavailable quantities stay visible.
- As a user, I want to undo a purchase so that changing my mind at checkout restores the outstanding item.
- As a user, I want dated purchase history so that I can review what I recorded as bought without confusing it with delivery evidence.
- As a user, I want updates from another device to appear automatically while I shop so that I notice newly added items without refreshing the page.
- As a migration operator, I want a documented authority decision so that legacy imports cannot silently overwrite new-app shopping or Ingredient changes.

## 6. Quantity Interpretation & Consolidation

### Preserve source; interpret conservatively

- Preserve each contribution's original quantity, unit and notes text, Recipe/Step/item IDs, and Recipe-name snapshot. Do not write normalized quantities back to Recipes or migration provenance.
- Parse only complete supported numeric expressions: positive integers, decimals, simple fractions and mixed fractions. Do not use prefix parsing that turns `1-2` into `1` or `2 heaped` into `2`.
- Recognize case-insensitive, explicitly enumerated aliases for mass (`g`, `gram(s)`, `kg`, `kilogram(s)`), volume (`ml`, `millilitre(s)`, `l`, `litre(s)`, `tsp`, `teaspoon(s)`, `tbsp`, `tablespoon(s)`, `cup(s)`), and counts/container labels.
- Normalize mass to grams and volume to millilitres using decimal arithmetic. NZ teaspoon = 5 ml, tablespoon = 15 ml, cup = 250 ml; kilogram = 1,000 g and litre = 1,000 ml.
- Ingredient identity plus dimension determines safe consolidation. For counts, keep the canonical count/container label in the key; an unlabelled piece and a tin are not interchangeable.
- Ranges, unknown units, ambiguous embedded units, heaped/packed measures, zero/negative source quantities, blank quantities and "to taste" remain unresolved contributions with the original text visible. They never silently become zero, one, or a validated physical quantity.
- If a recipe explicitly indicates another measurement standard, do not reinterpret it as NZ: retain it unresolved for review. The UI states the NZ assumption for ordinary unqualified cup/spoon units.
- No mass/volume conversion, ingredient density, piece weight, volume nutrition calculation or unit guessing from a product URL.
- Unknown units may share an Ingredient display grouping, but not a numeric total. Keep unresolved lines/contributions distinguishable and visibly excluded from any known total.

### Examples and required outputs

| Given contributions for the same linked Ingredient | Consolidated requirement | Purchase implication |
|---|---|---|
| `1 tbsp` and `2 tsp` | `25 ml`, with both original contributions accessible | User chooses packs/items or an explicit measured purchase; no assumed pack size. |
| `2 tbsp` and `1 tsp` | `35 ml` | Same rule; no Ingredient density required. |
| `1 cup` and `50 ml` | `300 ml` under the NZ assumption | A standard is stated, not silently inferred from nationality or recipe source. |
| `0.5 kg` and `250 g` | `750 g` | No grams-to-packs conversion. |
| `1/2 tbsp` and `1 1/2 tsp` | `10 ml` | No intermediate whole-number rounding. |
| `2 tins` and `1 tin` | `3 tin` in the canonical numeric model; user-facing pluralization is grammatical | Proposed purchase count is 3 tins. |
| `1 tbsp` and `20 g` | Separate `15 ml` and `20 g` requirements | Cannot consolidate across dimensions without Ingredient-specific information. |
| `1-2 tsp` and `1 tbsp` | Unresolved `1-2 tsp` plus known `15 ml` | No false complete numeric total. |

### Requirements are not purchase quantities

- A Grocery Item exposes known requirements and unresolved contributions separately from `OutstandingPurchaseQuantity` and `PurchaseUnit`.
- No physical amount is automatically treated as a number of packs. P2 requires explicit review/confirmation of a suggested purchase target before committing a Recipe addition.
- When an addition joins an existing line, show the existing and proposed purchase target. The user confirms an increment or keeps the existing target; do not automatically add one pack for every Recipe.
- Purchase units support explicit count labels and measured grams/millilitres. All purchase amounts are numeric; ad-hoc descriptive requirements can coexist with an explicitly chosen purchase target.
- Purchase events consume the outstanding purchase target, not an inferred fraction of Recipe requirements. Buying one of two packs does not prove that half the recipe's grams were obtained.
- Partial purchases subtract the recorded amount in the same purchase unit. Do not change that unit while a line has purchase history; create a separate line for a different purchase unit.
- Requirements remain source evidence; completion means the chosen shopping target was recorded purchased, not proof of precise ingredient mass, calorie intake or delivery.
- Store decimal values without float accumulation. Render sensible precision while retaining the unrounded stored amount; finite precision/overflow validation must reject unrepresentable input rather than silently rounding a supported quantity to zero.

## 7. Technical Requirements & Data Schema

### Aggregate and identities

Extend the existing `AppDbContext` through EF Core migrations, with one entity configuration per entity. Use GUID v7 application-assigned IDs. Do not expose entities or provenance through public DTOs.

| Entity | Suggested fields / relationships | Required invariant |
|---|---|---|
| GroceryList | `Id`, `OwnerUserId`, UTC created/updated times, `Version` | One per owner via unique index; creation is concurrency-safe. Every command updates the list version, including child/event writes. |
| GroceryItem | `Id`, `GroceryListId`, `OwnerUserId`, optional `IngredientId`, name/normalized-name snapshot, optional note and ad-hoc store mapping, consolidation dimension/count label, `PurchaseUnit`, `OutstandingPurchaseQuantity`, state, UTC audit times | Stable line ID distinct from Ingredient ID. Nonnegative outstanding quantity; removed/completed rows retained when referenced by history. No automatic catalogue writes. |
| GroceryContribution | `Id`, `GroceryItemId`, `OwnerUserId`, origin Manual/Recipe, optional `RecipeAdditionId`, original text fields, interpreted decimal amount/dimension or unresolved reason, source name/IDs snapshot, outstanding/completed/cancelled state | Each original requirement stays traceable. Source data survives later Recipe/Ingredient changes. A contribution is not a purchase. |
| GroceryRecipeAddition | `Id`, `GroceryListId`, `OwnerUserId`, `RecipeId`, Recipe name/version snapshot, UTC added time | Distinguish intentional repeated additions; never collapse them into an overwritable map keyed only by Recipe ID. |
| GroceryPurchaseEvent | `Id`, `GroceryListId`, `GroceryItemId`, `OwnerUserId`, positive quantity, purchase unit, UTC occurred time, name/note/store/source snapshots, origin, optional `ReversesEventId` | Append-only purchase or reversal. Reversal references exactly one same-owner purchase and is unique for that purchase. PRD05 writes manual origin only. |
| GroceryCommandReceipt | Owner/list, command UUID, command type/request fingerprint, committed result identities/version | Persist with the business transaction. A repeated identical command has one effect; same UUID with different content is a conflict. Not a generic event bus. |

The implementation may simplify physical columns or use owned values, but must preserve these identities, ownership constraints and observable invariants. Choose and document decimal precision/scale during S1, with fixtures covering every example in section 6 and the accepted limits.

### Ownership and integrity

- Use the existing current-user service; filter before joins, counting, grouping or history access.
- Enforce same-owner references for Ingredients, Recipes, list children and reversal targets at application and database-integrity boundaries. Use the repository's owner-aware FK pattern.
- Archived Ingredients remain readable in existing lines/history; hide them from new-Ingredient pickers. Grocery-excluded Ingredients cannot be newly selected for a linked grocery addition.
- History snapshots remain unchanged after an Ingredient rename, archive, mapping change or later Recipe import. Current outstanding display may resolve the latest Ingredient name/store mapping without rewriting history.
- No destructive cascade from Recipe/Ingredient changes into Grocery history. Referenced rows cannot be hard-deleted through incidental maintenance.
- Use normalized-name rules from Ingredient Maintenance for unlinked identity and lookup. A name match does not substitute for a linked Ingredient FK.
- Prevent duplicate active consolidation keys under concurrency. Reuse a completed line only where identity and purchase unit agree; retain distinct purchase events and Recipe-addition identities.
- Index owner/list/state lookups, Ingredient and Recipe references, owner/history UTC date plus event ID, and reversal/command uniqueness. Avoid per-item database queries.

### Purchase-event semantics

- Purchase command appends an immutable event and decrements the outstanding quantity in one transaction. Full completion leaves the line available to history but hidden by default.
- Full completion retires that line's current outstanding contribution associations; partial completion leaves them outstanding without claiming proportional physical fulfillment. Record the affected contribution IDs in the event snapshot. Later manual/new-Recipe additions do not reactivate old completed associations, so an unrelated re-addition cannot trigger a false repeat-Recipe warning.
- Explicit removal cancels only outstanding demand; it appends no purchase. Existing purchases remain visible and reversible.
- Undo appends a compensating reversal and restores exactly the purchased quantity in one transaction. It does not erase the purchase or recreate previously cancelled unrelated demand.
- Undo restores source associations affected by that event only, and only where they have not subsequently been explicitly cancelled. The restored shopping quantity remains valid even when its Recipe association was separately cancelled; retain historical attribution without resurrecting cancelled Recipe demand.
- If the original line was removed, explicitly explain that undo will reopen that line for the reversed quantity. Do not merge by name or silently resurrect its whole old target.
- A changed/cancelled source Recipe does not make a recorded purchase vanish. New purchases of the same item are separate events even on the same date.
- Record selected store when supplied. An unknown store stays absent, not guessed from grouping state or product URLs.
- A future OnlineCart event must remain distinguishable from an in-person recorded purchase. Neither event represents an independently verified delivery.

## 8. Application Workflows

### Add a Recipe

1. Offer "Add to grocery list" on active Recipe View. Preserve read-only recipe content and current cooking-progress state; no Recipe save occurs.
2. Server resolves same-owner active Recipe and eligible current Ingredient references. Include only typed Ingredient items not marked `ExcludeFromGroceryList`; skip Heading and Note, without legacy name-pattern filtering.
3. Archived linked Ingredients may contribute from an existing Recipe, with an explicit archived indication. Cross-owner/missing references block the whole command rather than silently dropping requirements.
4. Preview included, excluded and unresolved contributions, consolidation, and purchase-target suggestions. A Recipe with no eligible items shows an explicit no-items message and performs no addition.
5. If the Recipe contributes any outstanding line through an uncancelled addition, ask whether another Recipe's worth is intended. Cancelling leaves everything unchanged. Intentional confirmation creates a distinct addition.
6. Commit the reviewed source Recipe/list versions, confirmed purchase targets and explicit repeat decision atomically. A changed preview requires fresh review.
7. Show a success notice and link to Grocery List. Once previous demand is completed/cancelled, adding again creates new outstanding demand without rewriting purchase history.

Provide a "Remove this recipe's outstanding requirements" action for contributing additions. Preserve other Recipes, manual demand and purchase history. Because packs are independent of requirements, preview affected lines and require an explicit choice to retain, reduce or cancel each affected purchase target. Do not derive pack subtraction from recipe grams.

### Manual single and batch entry

- Support Ingredient lookup and ad-hoc text; each row has a note and explicit positive purchase quantity/unit.
- Use one single/batch entry modal, starting with one row. A keyboard-accessible "Add row" button with a plus icon appends a row; rows can be removed without clearing the remaining inputs, subject to the reviewed batch limit.
- Show an unchecked "Save to master ingredient list" checkbox per new unlinked row. Each checkbox operates independently; single and batch submit use the same validation and transaction policy.
- Checking a row's checkbox expands that row's Category and Departments fields within the modal. Both are optional and use the existing shared lookup/mapping controls. Unchecked new rows do not display these fields. Unchecking excludes any previously entered catalogue values from validation and saving; hidden values must not create Category, Department or Ingredient records.
- Selecting an existing Ingredient replaces the new-Ingredient checkbox with a clear linked-Ingredient indication, avoiding duplicate creation. It does not expose the new-Ingredient Category/Departments fields for editing that Ingredient. Editing a linked Grocery Item uses the selected-supermarket department workflow below.
- Selecting an active Ingredient links it; matching text alone does not. Display an existing same-owner match as a choice instead of silently changing identity.
- A checked row creates the minimal valid Ingredient using existing defaults and validation; optional category/store mapping values reuse existing catalogue rules. Rich nutrition/product-link editing remains in Ingredient Maintenance.
- A concurrent active-name collision returns `409` with an explicit select-existing action; an archived collision exposes the existing restore workflow. No automatic restore, overwrite or field merge.
- Batch rows with the same normalized new Ingredient name may explicitly share one creation if their catalogue fields agree; contradictory fields are validation errors. Never arbitrarily choose one row.
- All selected Ingredient creations and Grocery additions commit together. Do not call the current standalone Ingredient endpoint and then attempt the Grocery write.
- Editing a Grocery Item linked by Ingredient ID exposes only its department for the currently selected supermarket as an editable master-Ingredient field, separately from shopping-only fields such as note, purchase quantity and purchase unit. Ingredient name, category, other supermarkets' departments, nutrition and product links are not editable here; use Ingredient Maintenance for those changes. An ad-hoc item may still have an item-local department. This restriction does not remove Category/Departments fields when explicitly creating a new Ingredient.
- Label the department field and confirmation with the selected supermarket. Bind the edit to that store's stable ID; if the selection changes while editing, require fresh review for the newly selected store rather than applying the old value to it. If no supermarket is selected, do not permit master-department editing.
- If that department changes, show a before/after prompt explaining that updating the master Ingredient changes its department for this supermarket wherever it is used. Offer "Update master Ingredient", "Discard department change and save shopping changes", and Cancel. Do not silently discard the change or imply that a declined change was saved locally.
- Approval updates the same linked Ingredient's selected-store mapping by Ingredient/store IDs, never by name or by creating a replacement Ingredient. Reuse Ingredient Maintenance department validation, ownership and optimistic concurrency. Include the loaded Ingredient version as well as the list version; a conflict preserves the draft and requires renewed review.
- Commit the approved department update and shopping edits in one transaction. Preserve name, category, other stores' mappings, nutrition, product links and all other unrelated Ingredient fields. Shopping-only edits bypass the prompt and leave the master Ingredient unchanged. History snapshots and original Recipe contribution text remain unchanged in every branch.
- Preview duplicate consolidation and purchase-target changes. Keep entered data on validation/concurrency/network errors and require explicit review for identity changes.

### Shopping, removal and undo

- Default view contains only outstanding, noncancelled lines. "Mark purchased" defaults to all outstanding quantity; a partial option accepts a smaller positive amount in the same purchase unit.
- Completed lines hide immediately after confirmed success; show an accessible notice with Undo. Do not optimistically claim success before the server confirms it.
- "Show completed" exposes completed rows/events for the selected date scope; partially purchased lines stay outstanding and expose their recent events.
- History lists purchases and reversals with effective/reversed state, amount/unit, item snapshot, store and origin. Server-side paging and date filters; default today, selectable all dates. Reversals outside a date range must still determine the original event's current reversed state.
- Restore exactly one event with Undo, even after midnight or a later purchase. Repeated undo has no additional effect.
- "Remove item" and "Clear outstanding list" are cancellation actions with confirmation, not purchases. Neither clears history. Do not introduce a destructive "clear history" action.

## 9. API Contracts

All routes require authentication, DTOs, cancellation propagation and centralized RFC 7807 Problem Details. Route names are proposed; settle exact request/response types in S1 before implementation.

| Method | Route | Purpose / success |
|---|---|---|
| `GET` | `/api/grocery-list?includeCompleted=false&from=&to=` | Owner's list/version, outstanding items, requirement/source details and selected completed scope; `200` with empty state if never used. Date filters affect completed visibility, not outstanding items. Read does not create rows. |
| `POST` | `/api/grocery-list/items` | Atomic single/batch addition and optional Ingredient creation; `201` with `Location` and result/list version. |
| `PUT` | `/api/grocery-list/items/{id}` | Save shopping edits and an explicitly approved linked-Ingredient department change for the selected supermarket atomically using reviewed list/Ingredient versions; `200` with updated result/versions. |
| `DELETE` | `/api/grocery-list/items/{id}` | Cancel outstanding demand without deleting historical records; `204`. |
| `DELETE` | `/api/grocery-list/items` | Explicitly confirmed cancellation of all outstanding items, requiring list version; `204`. |
| `POST` | `/api/grocery-list/recipe-preview` | Read-only eligible Recipe/target preview with source/list versions; `200`; no rows or side effects. |
| `POST` | `/api/grocery-list/recipe-additions` | Commit reviewed Recipe addition, targets and repeat decision; `201` with addition identity/version. |
| `POST` | `/api/grocery-list/recipe-additions/{id}/cancel` | Cancel reviewed outstanding contributions/targets; preserve history and other sources; `200`. |
| `POST` | `/api/grocery-list/items/{id}/purchases` | Full/partial purchase from explicit reviewed amount and version; `201` with event identity/version. |
| `GET` | `/api/grocery-list/purchase-history` | Paged date-filtered events, snapshots and reversal state; `200`. |
| `POST` | `/api/grocery-list/purchase-history/{id}/reverse` | Append one reversal/restore quantity; `201` first time; `200` for an already-reversed purchase with its existing reversal. |

- Use GUID-constrained identifiers. Unknown/other-owner IDs return indistinguishable `404`; anonymous returns `401`.
- Invalid quantities, units, dates, limits and conflicting batch fields return `400` with field errors. Stale versions, changed Recipe preview, repeat intent missing and catalogue-name collisions return `409` with specific recoverable details.
- Every mutation carries a command UUID; updates/purchases/removals carry the reviewed list version. For DELETE use explicit request headers rather than relying on a request body.
- Linked-Ingredient updates require explicit department-update approval, the linked Ingredient ID/version, the reviewed selected supermarket's store ID and the proposed department. The edit contract allows only that store's department update, not a full Ingredient replacement or other catalogue changes; reject attempts to update disallowed fields or additional store mappings with `400`. Omitted department updates leave the Ingredient unchanged. Stale Ingredient/list versions return `409` and roll back the combined edit; invalid or other-owner department references return `400`. The client must bind the store ID to its reviewed selection; the server validates ownership and the store/department references without relying on browser state.
- Check a committed identical command receipt before rejecting its originally supplied stale version. Replay returns its recorded result and does not repeat mutations; UI reloads the current list. Different content with the same UUID returns `409`.
- Client retries an uncertain request with the same UUID and identical payload; genuinely new user intent uses a new UUID. Explicitly surface an uncertain outcome rather than reporting a false failure/success.
- Require an explicit numeric purchase amount even for Buy all, so a changed remainder cannot silently change the submitted purchase.
- History page defaults to 1 and page size 25, allowed 1-100; stable order UTC occurred time descending then event GUID. Invalid filters return `400`; expose total count.
- History uses an inclusive start and exclusive end UTC instant, derived from chosen `Pacific/Auckland` calendar dates using the offsets applicable on those dates. Do not use the device timezone, a fixed NZ offset or assume a day is always 24 hours. Queries include events in range; their reversal state is evaluated across all dates.
- No API accepts an arbitrary owner, trusts client recipe contents, accesses supermarket websites or marks purchases by ingredient name.

## 10. Web Requirements

- Replace `/grocery-list`; add `/grocery-list/history` under the existing shell. Document titles use existing conventions; preserve menu ordering and all deferred stubs.
- Put new UI under `apps/web/src/features/grocery-list/`, with typed services, Zod boundary schemas and dedicated hooks. Reuse shared API client, errors, title-case helper, dropdowns, pagination and responsive layout.
- Desktop grouping/list and mobile cards expose the same essential actions. Use the existing 700 px breakpoint and shared spacing; do not create a second global stylesheet or monolithic component.
- Fixed choices use native selects; Ingredient typeahead uses shared `LookupTypeahead`. Accessible labels, focus management, keyboard actions, and error/status announcements.
- Default group by selected store's department and then title-cased name, with stable item-ID tie-break. Persist selected seeded store locally; no additional store-management screen.
- Resolve current linked mappings for display. Unmapped/ad-hoc-without-mapping items show "Uncategorised"; not stocked remains explicitly visible as "Not stocked here", not silently hidden or purchased.
- Clearly label requirement totals, unresolved source text, purchase quantity/unit, partial outstanding amounts and purchase-origin/history state.
- Recipe-source details show each addition and its original contributions; users can open the existing Recipe View. An archived/unavailable Recipe does not remove its snapshot or historical contribution.
- Name display/edit values use shared title case on load/blur, not every keystroke; do not rewrite stored legacy names in bulk.
- Provide distinct never-used/empty, all-completed, no-filter-results, loading and error/retry states. Preserve outstanding screen content and drafts during failed writes.
- Poll the authoritative Grocery List read endpoint every 15 seconds while its page is visible; do not require manual refresh to discover another device's changes. Use cancellable reads without overlapping requests or stale responses overwriting newer mutation results.
- Pause polling on document visibility becoming hidden (including normal phone lock/background behavior), route exit, logout or account change. On visibility return, page restoration or reconnection, perform an immediate read and restart the interval without duplicate timers. Page focus may trigger the same deduplicated refresh. A browser can suspend a page before notification; no background execution guarantee is required.
- Apply incoming changes automatically to the nonedited list and show a brief accessible "Grocery list updated" notice when displayed data changes, not on every unchanged poll. Preserve scroll/focus where practical. Do not overwrite or reset an open dirty form; show that newer data is available and require fresh review/version validation before applying conflicting edits.
- Show the last successful check and a visible "Unable to check for updates" indication after failed checks. Keep the last known list readable but do not imply it is current; retry at the polling interval while visible and immediately on reconnection. A browser online event alone does not establish synchronization: clear stale/error state only after a successful authoritative read.
- Clear owner-scoped state and stop polling on logout/account change; existing session-expiry routing remains authoritative. No polling of inactive feature routes or claim of instantaneous Firestore-style synchronization.
- Show completed/history date scope and purchase units plainly. Never label manual/OnlineCart history as confirmed delivered goods.

## 11. Legacy Migration, Database Authority & Cutover

This feature does not depend on a live production database or an actual-data import. Automated tests seed synthetic data into isolated PostgreSQL; local development uses the existing persistent Docker database.

Before production usage:

1. Start the new Grocery List empty without importing legacy current demand, completions or purchase history. The user can enter the small current list manually; keep real data outside Git and ordinary logs.
2. Decide the authoritative application for Recipes, Ingredients and shopping data during transition. No ongoing dual-write/three-way-merge system is introduced.
3. Rehearse final Ingredient/Recipe import, same-owner references and PostgreSQL backup/restore. Existing import approvals can replace authored fields; they do not infer which application's change should win.
4. Verify that Grocery additions and purchase history persist across reloads, navigation and API/database restarts. Do not confuse an initially empty list with disposable shopping data.
5. Do not manufacture historical purchases from legacy completion flags. PRD05 history starts with purchases actually recorded in the new application.
6. No legacy import may silently clear, replace or reinterpret new-app outstanding items, events or reversals. Preview conflicts and require an explicit migration policy.
7. Stop legacy edits for final cutover, verify restored database and existing image associations, then switch actual usage. Hosting alone does not solve authority conflicts.

Common/Pending migration and extension readiness are not prerequisites to local PRD05 acceptance. Tool removal and final legacy retirement remain gated by all required downstream migrations described in earlier PRDs.

## 12. Edge Cases & Failure Scenarios

| Scenario | Required behavior |
|---|---|
| Two devices purchase the same outstanding quantity | One reviewed mutation wins; the other returns `409`, with no negative outstanding value or duplicate effective purchase. |
| Response lost after commit | Identical UUID retry returns the committed result; no duplicate Ingredients, additions or events. |
| Second Recipe click after preview changes | Refresh and reconfirm; no blind duplicate addition or automatic approval. |
| Unsupported source expression | Retain original text and unresolved reason; require an explicit shopping target, not a successful fabricated total. |
| Ingredient archived/excluded after selection | Revalidate eligibility before commit; preserve existing demand/history and explain any blocked new addition. |
| Purchase undone after explicit item removal | Explain reopening; restore only that event's quantity, not cancelled unrelated demand. |
| Partial purchase and later addition on the same line | Preserve distinct events and new demand; undo affects only the selected purchase. |
| Reversal occurs tomorrow | Original purchase still displays as reversed when viewing yesterday; no date-based lockout. |
| One batch row invalid or Ingredient insert races | Roll back the complete command; preserve all input, identify rows/conflicts explicitly. |
| Recipe/import changes existing Steps | Shopping uses recorded contributions, not positional live reconciliation. Require new preview for a new addition; do not rewrite existing demand/history. |
| API/database unavailable or quota failure | Explicit standard errors and logging; no success-shaped fallback or locally claimed committed purchase. |
| Offline while shopping | Display unavailable/failed-save state; no queued offline mutations or claim of synchronization. |
| Another device edits the list while it remains open | Next successful visible-page poll applies changes and announces them; dirty forms are preserved and conflicts require review. |
| Phone locks or page is backgrounded | Polling pauses when hidden/suspended; immediately fetch on return before resuming the interval, without accumulating duplicate timers. |
| Poll fails or an older response arrives late | Display stale/error state and retain readable data; no stale response replaces newer committed state or silently resets an edit. |

## 13. Acceptance Criteria

Unit = xUnit/FluentAssertions for backend and Vitest for frontend; BDD = Reqnroll against isolated PostgreSQL; E2E = Playwright. Shared criteria may require evidence at multiple layers.

### Foundation, ownership and quantities

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-01 | An owner without a list | They read it, then issue concurrent first additions | Read returns an empty view without writes; exactly one persistent list exists after accepted commands | BDD |
| AC-02 | Two owners and anonymous requests | List/item/source/history routes or linked references are accessed | Anonymous returns 401; other-owner resources return 404; cross-owner links cannot commit or disclose data | BDD |
| AC-03 | Two Recipes reference one Ingredient as 1 tbsp and 2 tsp | Reviewed additions commit under NZ measures | One volume requirement totals exactly 25 ml; both original texts and source identities remain available; source Recipes are unchanged | Unit + BDD + E2E |
| AC-04 | The numeric examples in section 6, including mixed fractions | Quantities consolidate | Outputs are exactly 35 ml, 300 ml, 750 g, 10 ml and 3 canonical tins respectively; no intermediate rounding | Unit + BDD |
| AC-05 | Same Ingredient in mass, volume, different count labels and unresolved expressions | Additions are previewed/committed | Incompatible requirements stay separate; unresolved text is visible and excluded from known totals; no density/pack assumptions occur | Unit + BDD + E2E |
| AC-06 | Range, embedded, heaped, blank, zero/negative, invalid fraction or known non-NZ source units | Interpreter runs | Explicit unresolved results preserve text; no prefix parsing, divide-by-zero, silent zero/one or NZ reinterpretation | Unit |
| AC-07 | Linked and unlinked items with equal names and compatible units | Consolidation runs | Linked identity uses Ingredient GUID; unlinked uses normalized name; no silent catalogue linking; concurrent active keys do not duplicate | Unit + BDD |
| AC-08 | A 45 ml requirement and existing purchase target | Another Recipe is added | Requirement and purchase target are separately labelled; user confirms keeping/incrementing the target; 45 ml never means 45 packs | Unit + BDD + E2E |
| AC-09 | Purchase inputs of 0, negative values, 9,999 and values above 9,999 across count/measured units, plus supported measured decimals | API and UI validate, or consolidation/Undo exceeds the input limit | Entered quantities must be greater than zero and at most 9,999; measured decimals retain supported precision; requirement totals and consolidated/restored demand are not capped or truncated; above-limit outstanding balances remain visible and purchasable through Buy all without permitting above-limit edited input | Unit + BDD |

### Recipes and manual entry

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-10 | Active Recipe with Ingredient, Heading, Note and grocery-excluded entries | Added through Recipe View | Only eligible typed Ingredient entries contribute; exclusions are explained; no name regex filtering, Recipe writes or cooking-progress reset | BDD + E2E |
| AC-11 | Existing Recipe references an archived Ingredient, or has no eligible items | Preview opens | Archived source is visibly retained; zero-eligible preview explains no addition and writes nothing | BDD + E2E |
| AC-12 | A Recipe already contributes outstanding items | User adds it again and cancels or confirms | Cancellation changes nothing; confirmation creates a distinct addition and reviewed demand, retaining prior history | BDD + E2E |
| AC-13 | Prior Recipe demand is completed/cancelled and its old line is later reused manually | Recipe is added again | New demand is created without a false repeat-outstanding warning or alteration of old events; manual reuse did not reactivate old Recipe associations | BDD |
| AC-14 | Recipe/list changes after preview | Commit is attempted | 409 requires fresh review; no unreviewed source or target applies | BDD + E2E |
| AC-15 | An addition shares a line with another Recipe/manual demand and purchases | Its outstanding contributions are cancelled | Explicit target review preserves other sources and all purchases; no inferred grams-to-pack subtraction | Unit + BDD + E2E |
| AC-16 | One or several new manual items | Saved with catalogue choices unchecked | Grocery additions commit, no Ingredients are created, and defaults/notes/identity follow the same single/batch rules | BDD + E2E |
| AC-17 | Single and batch rows explicitly opt into catalogue saving | User adds/removes rows, toggles individual checkboxes and submits | Only checked new rows expand optional Category/Departments fields; unchecking hides and excludes their catalogue values from validation and persistence; existing selections show linked status instead of a creation checkbox; valid submission creates selected Ingredients and Grocery items atomically, unchecked new rows stay ad-hoc, and failed saves retain all remaining row inputs with field errors | Unit + BDD + E2E |
| AC-18 | Batch rows share a normalized catalogue name | Fields agree or conflict | Agreed explicit creation uses one Ingredient; contradictory values return row errors and zero command writes | Unit + BDD |
| AC-19 | Concurrent active/archived catalogue-name collision or stale linkage | Save is attempted | 409/validation explains select-existing/restore/reload as appropriate; no silent update, restore or partial batch | BDD + E2E |
| AC-20 | A Grocery Item linked by Ingredient ID is edited with a selected supermarket | User changes its department and approves, declines or cancels the prompt | Only the selected supermarket's master department is editable; approval saves that mapping and shopping edits atomically; decline explicitly discards the department change and saves shopping edits only; Cancel retains the draft without writes; shopping-only edits do not prompt; name/category/other stores and history/source snapshots remain unchanged | Unit + BDD + E2E |
| AC-40 | A linked department edit reviewed at list/Ingredient versions and selected store, including absent/changed store selection or disallowed catalogue fields | Selection changes, concurrent writes occur, or an invalid request/save fails | No master edit without a selected store; changed selection requires fresh review; disallowed fields/additional store edits/invalid references return 400; stale versions return 409; failures write nothing and retain the draft; unrelated master fields remain intact and identical replay cannot apply twice | Unit + BDD + E2E |

### Purchases, cancellation and history

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-21 | Three outstanding tins | Mark purchased uses default amount | One event records three tins atomically, outstanding becomes zero, item hides and success notice offers Undo | BDD + E2E |
| AC-22 | Three outstanding tins | One tin is recorded purchased | Event records one, two remain outstanding in the same unit, and recipe physical requirements are not falsely recalculated | Unit + BDD + E2E |
| AC-23 | Zero, negative, excessive or wrong-unit purchase input | Submitted | 400/409 as appropriate; no event, no negative remainder and no changed source requirements | Unit + BDD |
| AC-24 | Full/partial purchases across several dates | Completed/history scope changes | Outstanding default remains uncluttered; date scope is visible; all-dates is available; no midnight loss/reset | BDD + E2E |
| AC-25 | A purchase, later additions and optionally removed original line | User reviews and confirms Undo | Exactly one compensating event restores only purchased quantity; original purchase/snapshot survives; unrelated cancelled demand is not restored | Unit + BDD + E2E |
| AC-26 | Purchase reversed outside its original date range | Original date history is viewed or Undo repeated | Purchase shows reversed state; repeated undo does not restore twice or erase events | BDD |
| AC-27 | Outstanding and completed items | User removes one or clears outstanding list | Confirmation cancels outstanding demand only; no fabricated purchase and no history deletion | BDD + E2E |
| AC-28 | Ingredient/Recipe/store metadata changes after purchase | History and outstanding list load | History retains original snapshots; current display can resolve latest names/mappings without rewriting events | BDD |
| AC-29 | Purchases around Auckland midnight/daylight-saving transitions and a browser in another timezone | Dates display and chosen NZ dates become query boundaries | Display/default today use Pacific/Auckland; correct inclusive-start/exclusive-end UTC range and deterministic paging; no device-timezone, fixed-offset or 24-hour-day assumption | Unit + BDD + E2E |
| AC-30 | Two devices and a lost post-commit response | Concurrent/stale writes and identical UUID retries occur | Version conflict prevents lost updates; replay has one effect; changed payload under reused UUID returns 409 | BDD |
| AC-31 | A failure during catalogue/event/contribution persistence | Transaction fails | All command changes and receipt roll back; explicit error is shown/logged; entered data is retained | BDD + E2E |

### UI, regression and transition

| ID | Given | When | Then | Test Type |
|---|---|---|---|---|
| AC-32 | Mapped, unmapped, not-stocked and ad-hoc items | Seeded store selection changes | Correct stable department/name grouping; not-stocked items remain explicit; selected store persists locally | Unit + BDD + E2E |
| AC-33 | Lowercase names and single/batch lookup fields | Viewed, edited and blurred | Shared title case and dropdowns apply without per-keystroke rewriting or bulk stored-data changes | Unit + E2E |
| AC-34 | 375 px and 1280 px viewports with keyboard navigation | Primary workflows run | Add Recipe/manual, quantity review, partial purchase, completed/history and Undo are accessible with equivalent essential behavior | E2E |
| AC-35 | Loading, empty, all-completed, no-results, failed read/write and expired session | Views/actions run | Distinct states and explicit retry/session handling; no false committed success, lost dirty form on refresh or owner-state leakage | Unit + E2E |
| AC-36 | Existing auth, navigation, Ingredient maintenance, Recipe browsing/images and migration tests | PRD05 integrates | Intended existing behavior remains; only Grocery stub and explicit Add to grocery action change; deferred destinations stay stubs | BDD + E2E |
| AC-37 | Synthetic seeded Recipe/Ingredient and isolated PostgreSQL | End-to-end add/shop/partial/undo/reload journey runs | Persistent counts, totals, source identities and history match requirements without mocked mutation APIs or real personal data | BDD + E2E |
| AC-38 | An owner first opens Grocery List and then adds items/records purchases | They navigate away, reload and restart API/database | Initial list is empty without legacy import; subsequently recorded outstanding items, purchases and history persist; authority/cutover guidance preserves new-app data | BDD + E2E |
| AC-39 | Existing persistent development data | Database migrations and focused regression run | Existing Ingredient/Recipe IDs, text, provenance and images are preserved; no Firestore or provider change is required to develop PRD05 | BDD |
| AC-41 | A visible list and changes made from a second device | A 15-second polling interval elapses and its read succeeds | Updates appear automatically with a change notice; unchanged reads do not announce changes; no overlapping reads, stale-response rollback or dirty-form overwrite | Unit + BDD + E2E |
| AC-42 | Polling page becomes hidden, restored, disconnected, reconnected or exits/logs out | Lifecycle and connectivity events occur | Hidden/exited polling stops; return/reconnection fetches immediately and resumes one interval only while visible; failed checks show stale state/last success; only a successful read clears that state; expired sessions stop owner-scoped polling | Unit + E2E |

## 14. Implementation Slices, Tests & Definition of Done

### Proposed issues and prerequisites

| Slice | Proposed issue / scope | Acceptance evidence | Prerequisites |
|---|---|---|---|
| S1 | Grocery aggregate, quantity contracts and persistence foundation | Implement accepted P1-P6 decisions and uniform purchase-input limits without truncating aggregation/Undo; failing numeric/identity/history-invariant tests; precise DTO/decimal contracts; EF migrations, ownership keys, version/receipt rules; AC-01/02/03-09/25/26/30/39 foundation | PRD03, PRD04 |
| S2 | Owner-scoped reads, store grouping and responsive outstanding view | Empty/loading/error states, shared controls, NZ date handling, lifecycle-aware visible-page polling and stale/change notices; AC-02/24/28/29/32-35/41/42 read coverage | S1 |
| S3 | Atomic manual single/batch additions, optional Ingredient creation and prompted selected-store department editing | Transaction-bound reuse of existing validation/persistence; department-only update prompt, selected-store binding, dual-version checks, duplicate and rollback handling; AC-07/08/09/16-20/30/31/40 | S1 |
| S4 | Recipe preview, confirmed additions and source cancellation | Typed eligibility, source snapshots, unit consolidation and reviewed purchase targets; AC-03-15/30/31 | S1, S3 purchase-target contracts |
| S5 | Purchases, partial completion, cancellation, history and Undo | Immutable events/reversals, date scopes, completed view, safe retries; AC-21-31/35 | S1, S3 |
| S6 | Integrated UI journeys, regressions and transition guidance | Wire S3-S5 to S2; synthetic real-API E2E, accessibility, persistence reload, documented start-empty/cutover policy; AC-34-39/41/42 plus cross-slice coverage | S2, S3, S4, S5 |

S2 and S3 may progress independently after S1 contracts; S4 and S5 can progress in parallel after S3's addition/target contracts. None depends on production imports. Create GitHub issues only after reviewing this draft, then replace placeholders with actual epic/sub-issue and PR links.

### Test strategy

- Write failing tests before implementation. Extend existing xUnit/FluentAssertions, Reqnroll, Vitest and Playwright projects; do not introduce another testing stack.
- Unit fixtures assert exact output shapes, decimals and unresolved reasons, not only that two rows became one. Include quantity examples, parsing rejection, canonical aliases and count-label separation.
- Reqnroll covers owner boundaries, real PostgreSQL constraints, transaction rollback, concurrent first list/name/add/purchase writes, stale previews and command replay.
- Model concurrency with separate contexts/devices; simulate response loss after a successful commit, not only failures before saving.
- Vitest covers typed boundaries, form retention, independent requirement/purchase presentation, Auckland date/DST boundaries, duplicate confirmation and completed/undo state. Use controlled timers for polling, visibility/reconnect events, nonoverlap and stale-response handling.
- Playwright verifies automatic second-device updates without manual refresh, preserved dirty forms and visible failed-check notices; test visibility lifecycle deterministically rather than claiming physical phone-lock coverage from a desktop browser.
- Playwright primary journeys use seeded API/database data, accessible selectors and auto-waiting, not arbitrary sleeps. Run desktop/mobile and purchase/undo reload paths.
- Retain focused Ingredient create/edit/import, Recipe import/read/image, authentication and navigation regressions for shared-context/service/UI changes. Existing CI must run the applicable lint/tests; no new cloud credentials are needed.

### Definition of done

- Draft recommendations are accepted or amended; all acceptance criteria have automated evidence or the explicitly identified manual review.
- Implementation issues and PRs link this PRD and applicable AC IDs; proposed issue placeholders are replaced accurately.
- New persistence is deployed through EF migrations; existing data and legacy source text remain intact.
- Same-owner integrity, exact compatible totals, explicit unresolved quantities and separate shopping targets are verified.
- Single/batch catalogue creation is consistent and atomic; existing linked Ingredients expose only the selected supermarket's department for prompted updates committed with shopping edits using list/Ingredient concurrency checks. Other master fields remain unchanged; unapproved master changes and partial-success batches are absent.
- Purchases, partial purchases, cancellation, immutable history, date views and Undo persist across reloads; retries/concurrency cannot duplicate effective changes.
- Docs explain local database authority, persistent empty startup without legacy-list import, NZ time, visible-page polling/staleness, retry/undo semantics and future OnlineCart meaning. No unreviewed production writes or scope creep.
- Components/classes remain within the project's 200-250-line guidance; shared helpers are reused and affected regressions pass.

## 15. Risks, Assumptions & Future Considerations

| Risk / future feature | Response |
|---|---|
| Recipes use different spoon/cup standards or ambiguous expressions | State NZ measures; keep explicit alternatives/unparseable values unresolved. Never silently reinterpret existing storage. |
| Grocery requirements are mistaken for purchase packs | Separate physical requirements, user-confirmed targets and purchase units; do not promise pack-size intelligence. |
| Food Diary needs grams for per-100 g nutrition | Later add Ingredient-specific conversion records with measurement/preparation, source and uncertainty; volume ratios alone do not determine grams. Preserve original requirements now. |
| Purchased/history is mistaken for delivered/consumed | Manual events are user assertions; future OnlineCart origin means cart addition. Order/delivery/intake events require separate verified workflows. |
| Repeated additions lose per-Recipe identity | Retain distinct Recipe-addition and contribution IDs, not a dictionary keyed only by Recipe ID or child position. |
| Legacy import overwrites local authored Ingredients/Recipes | Define authority and final cutover explicitly; moving to a hosted database does not remove import conflict risk. |
| Future authoring changes Recipes already added to a list | Existing contributions remain snapshots; a future PRD must define explicit refresh/replacement rather than positional automatic reconciliation. |
| Future Common/Pending workflow | Reuse stable item identities and commands. Selected Pending demand promotes before cart attempts and stays outstanding on failure, per D9. |
| Future Woolworths integration | Separate PRD for scoped authentication, confirmed per-item outcomes, safe retries and website-compatible interaction. No promise to bypass website restrictions. |
| History retention increases storage | Measure actual usage; no silent pruning. A future retention/export decision must preserve user understanding and reversal semantics. |
| No offline writes or server-push updates | Visible-page 15-second polling, immediate return/reconnection reads and stale notices provide automatic updates without persistent push connections. Offline writes or server push require separately reviewed scope. |
