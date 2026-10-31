@firestoreMigration
Feature: Standalone Firestore migration (PRD03, issue 42)
  Imports are reviewed outside the application and committed atomically.

  Background:
    Given a synthetic Firestore backup and an existing migration owner

  Scenario: AC-44 AC-45 AC-46 Dry-run is read-only
    When I preview the synthetic migration
    Then the migration database has no ingredients or lookups
    And the migration output identifies the destination and proposed additions

  Scenario: AC-60 AC-65 AC-66 Writes preserve source values and repeat cleanly
    When I write the synthetic migration and repeat it
    Then exactly one ingredient retains its source GUID and values
    And the repeated migration is unchanged with stable child IDs
    And the imported ingredient is readable through the authenticated API

  Scenario Outline: AC-49 AC-62 AC-65 AC-69 Complete updates require individual approval
    Given the synthetic backup has already been imported and archived
    When I review a replacement ingredient and respond <response>
    Then the ingredient update is <outcome> with audit and archive state preserved
    Examples:
      | response | outcome  |
      | yes      | applied  |
      | no       | declined |
      | EOF      | declined |

  Scenario Outline: AC-61 Stale plans cannot commit
    Given the synthetic backup has already been imported and archived
    When the <resource> changes during ingredient approval
    Then the reviewed plan is rejected without applying migration changes
    Examples:
      | resource    |
      | source      |
      | decisions   |
      | ingredient  |
      | link        |
      | mapping     |
      | lookup      |
      | insertion   |

  Scenario: AC-63 A mid-write failure rolls back additions and updates
    When a database write fails after some migration changes were saved
    Then the migration database has no ingredients or lookups

  Scenario: AC-64 Cancellation before commit leaves no writes
    Given the synthetic backup has already been imported and archived
    When I interrupt ingredient approval
    Then the reviewed plan is rejected without applying migration changes

  Scenario Outline: AC-47 AC-48 AC-50 AC-51 AC-58 AC-67 Invalid plans block writes
    When I attempt a migration with <problem>
    Then blocking migration diagnostics leave the target unchanged
    Examples:
      | problem                |
      | duplicate IDs          |
      | ownership conflict     |
      | archived name conflict |
      | unknown store          |
      | unknown chain          |
      | unclassified invalid   |
      | incompatible heading   |

  Scenario: AC-67 AC-68 Retained classifications distinguish ingredients, headings and notes
    When I import explicitly classified invalid items
    Then only the excluded ingredient is created and decisions are unchanged

  Scenario: AC-61 Concurrent inserts cannot bypass final verification
    When another writer attempts an insertion while the migration commits
    Then the migration commits and the competing write is blocked

  Scenario: AC-65 Preferred-link changes reuse children and satisfy unique constraints
    When I switch preference between two imported product links
    Then the new preference persists with the same link IDs

  Scenario: AC-69 Ingredients absent from newer backups are untouched
    Given the synthetic backup has already been imported and archived
    When I import an empty newer ingredient backup
    Then the absent ingredient keeps all values and its version

  Scenario Outline: AC-45 AC-59 Required files, schema and owner are checked before writes
    When a required <requirement> is unavailable for migration
    Then an explicit migration failure leaves no ingredient writes
    Examples:
      | requirement |
      | ingredients |
      | meta        |
      | decisions   |
      | owner       |
      | schema      |

  Scenario: PRD04 Recipe preview validates Ingredient ownership without writing
    When I preview a synthetic Recipe that references the existing Ingredient
    Then the Recipe preview reports a proposed insert without committing it

  Scenario: PRD04 Recipe preview reports a missing Ingredient without writing
    When I preview a synthetic Recipe that references a missing Ingredient
    Then the Recipe preview is blocked and still writes its report

  Scenario: PRD04 Recipe preview validates selected image bytes without uploading
    When I preview a synthetic Recipe with a valid image manifest
    Then the Recipe preview reports validated image metadata without committing it

  Scenario: PRD04 Recipe preview blocks corrupt selected image bytes
    When I preview a synthetic Recipe with a corrupt selected image
    Then the Recipe preview reports a blocking image diagnostic
