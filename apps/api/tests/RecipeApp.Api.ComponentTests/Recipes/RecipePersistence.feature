Feature: Recipe persistence
  As the recipe migration
  I want durable owner-scoped recipe records
  So that imported recipe content remains intact and safe to query

  Scenario: Recipe aggregate preserves ordered content and source metadata
    Given a recipe owner, an archived ingredient, and a recipe with ordered content
    When the recipe aggregate is saved and reloaded
    Then recipe text, ordering, repeated ingredients, and optional metadata are preserved

  Scenario: Recipe identities are exact and scoped to their owner
    Given two recipe owners
    When null, case-distinct, and cross-owner legacy identities are saved
    Then those identities coexist and exact duplicates and blank identities are rejected
    When a persisted recipe legacy identity and owner are changed
    Then the database rejects identity reassignment

  Scenario: Recipe item relationships enforce kind and ownership
    Given a recipe and an ingredient owned by different users
    When an ingredient item links across owners
    Then the database rejects the relationship
    When a structural item carries an ingredient reference
    Then the database rejects the item kind and reference mismatch
    When an ordered child uses a negative position
    Then the database rejects the invalid position
    When two steps use the same parent position
    Then the database rejects duplicate ordering
