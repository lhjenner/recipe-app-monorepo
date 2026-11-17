Feature: Recipe grocery additions
  As an authenticated user
  I want Recipe requirements added with their source attribution
  So that shopping demand remains reviewable and safely recalculated

  Scenario: Preview and add eligible Recipe requirements
    Given a logged in user
    And an active Recipe with eligible, excluded, archived, and non-Ingredient items
    When I preview the Recipe for grocery addition
    Then the preview includes only eligible source items and makes no writes
    When I add the reviewed Recipe
    Then compatible requirements consolidate and preserve their Recipe sources

  Scenario: Removing a Recipe preserves manual demand
    Given a logged in user
    And an active Recipe with countable onion demand to remove
    When I add this Recipe before adding a manual increment
    And I add one manual increment
    When I remove the Recipe's outstanding requirements
    Then manual demand remains without Recipe demand

  Scenario: Repeat additions require deliberate confirmation
    Given a logged in user
    And an active Recipe with countable onion demand
    When I preview the Recipe for grocery addition
    And I add the reviewed Recipe
    When I preview the Recipe for grocery addition
    Then the preview reports outstanding Recipe demand
    When I try to add the Recipe again without confirmation
    Then the repeat attempt is rejected without changing demand
    When I confirm adding another Recipe's worth
    Then a distinct Recipe addition is recorded

  Scenario: Removing the last measured Recipe keeps manual generic item demand
    Given a logged in user
    And an active Recipe with measured flour demand to remove
    When I add this Recipe before adding a manual increment
    And I add one manual increment
    When I remove the Recipe's outstanding requirements
    Then manual demand remains without Recipe demand

  Scenario: A Recipe changed after preview requires a fresh review
    Given a logged in user
    And an active Recipe with countable onion demand for stale-review testing
    When I preview the Recipe for grocery addition before it changes
    And the Recipe changes after preview
    When I add the Recipe reviewed before its change
    Then the Recipe addition is rejected as stale without writes
