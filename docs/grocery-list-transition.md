# Grocery List Local Authority and Cutover

This guide records the local transition policy for PRD05. It applies to development
and rehearsal only; it does not provision hosting or authorize production writes.

## Database authority

- PostgreSQL is the authority for the Grocery List, purchase events, reversals,
  Grocery/Ingredient links and Recipe contributions.
- The legacy app and PostgreSQL must not receive ongoing dual writes.
- A legacy Grocery List document is not an import source. It must not silently
  replace, merge into or clear new-app data.
- Legacy completion flags must not create purchase history. Start the new
  application with an empty outstanding list and an empty purchase history.
- Existing Ingredient and Recipe migration decisions remain separate. Stop legacy
  edits before final cutover, review required updates and verify the destination
  owner IDs and relationships.

## Persistent local startup

Use the persistent Docker volume and EF migrations described in the repository
README. The first Grocery List read does not create a list. A later manual or
Recipe addition creates one persistent owner-scoped list.

Navigation, browser reload and API restart must not reset it. Database restart
rehearsals preserve outstanding quantities and purchase history. Remove the
PostgreSQL Docker volume only when intentionally deleting local data.

## Rehearsal and cutover

1. Start from synthetic or deliberately entered local data. Never copy personal
   legacy data into repository files or logs.
2. Record a manual/Recipe addition, whole-line purchase and Undo in a synthetic
   owner account.
3. Navigate away and reload the web app. Restart the API and, if needed, PostgreSQL
   without removing the volume.
4. Verify the same outstanding quantities, source identities and purchase/reversal
   history after each restart.
5. Take a database backup and perform an isolated restore before changing the
   authoritative application.
6. Stop legacy writes. Run migrations and import approvals only against the chosen
   PostgreSQL destination. Resolve conflicts explicitly; do not infer a winner
   from timestamps or silently overwrite app-authored fields.
7. Verify representative Ingredients, Recipes, image associations, the empty
   Grocery startup and newly recorded shopping events before switching actual use.

## Runtime behavior

- Dates and history boundaries use `Pacific/Auckland`, not browser-local dates.
  Stored timestamps remain UTC.
- The Grocery List polls the authoritative read endpoint every 15 seconds only
  while visible. Hidden, exited or logged-out pages stop polling.
- Returning or reconnecting performs an immediate read. Failed checks retain the
  last readable list and remain explicitly stale until a successful read.
- Mutations use command UUIDs and list/ingredient versions. A lost response can be
  replayed with the same command; changed replay payloads are rejected.
- Purchases record the entire reviewed outstanding line. A later Undo appends one
  reversal and restores only the purchased quantity; it does not restore cancelled
  unrelated demand or erase history.
- Future cart integration may report OnlineCart origin, but PRD05 manual purchases
  do not verify delivery or payment.
