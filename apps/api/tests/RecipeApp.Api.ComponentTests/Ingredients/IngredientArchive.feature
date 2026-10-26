Feature: Ingredient archive and restore
  As an authenticated user
  I want to archive and restore ingredients
  So that old records remain available to existing references

  Scenario: Archive is idempotent and removes an ingredient from the default list
    Given a logged in user
    And the user owns an ingredient named "Archive Me"
    When I archive that ingredient twice
    Then both archive responses are no content
    And the ingredient is archived exactly once
    And the default ingredient list does not contain "Archive Me"
    When I restore that ingredient
    Then the response is no content
    And the ingredient is active in the default list

  Scenario: An archived ingredient cannot be silently changed by a stale edit
    Given a logged in user
    And the user owns an ingredient named "Stale Ingredient"
    When I save a stale edit after archiving the ingredient
    Then the stale edit is rejected and the ingredient stays archived

  Scenario: Restore is idempotent for an active ingredient
    Given a logged in user
    And the user owns an ingredient named "Already Active"
    When I restore that ingredient twice
    Then both restore responses are no content
    And the ingredient is active in the default list

  Scenario: Another user cannot archive or restore an ingredient
    Given a logged in user
    And the user owns an ingredient named "Private Archive"
    When I archive that ingredient
    And another user signs in
    Then archiving and restoring another user's ingredient return not found
    And the ingredient remains archived

  Scenario: Ingredient hard delete is not available
    Given a logged in user
    And the user owns an ingredient named "Retained Ingredient"
    When I delete that ingredient
    Then the response method is not allowed
