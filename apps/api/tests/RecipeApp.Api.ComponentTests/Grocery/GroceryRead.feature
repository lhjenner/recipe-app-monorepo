Feature: Owner-scoped grocery reads
  As an authenticated user
  I want to read my grocery list and purchase history
  So that another owner's data is never disclosed

  Scenario: Grocery list read does not create an empty list
    Given a logged in user
    When I request the default grocery list
    Then the grocery list is empty without a persistent list

  Scenario: Grocery list resolves current Ingredient metadata without changing source text
    Given a logged in user
    And the owner has a grocery item with a source requirement and current store mapping
    When I request the default grocery list
    Then the grocery item shows current Ingredient metadata and the original source text

  Scenario: Grocery list and history exclude another owner's data
    Given a logged in user
    And another owner has grocery items and a purchase event
    When I request the default grocery list
    Then the grocery list is empty without a persistent list
    When I request grocery purchase history
    Then grocery purchase history contains 0 events

  Scenario: History uses Auckland date boundaries and reversal state across dates
    Given a logged in user
    And purchase events span the Auckland date boundary for "2026-04-05"
    When I request grocery purchase history from "2026-04-05" to "2026-04-05"
    Then grocery purchase history contains 1 event named "In range" marked reversed

  Scenario: Grocery reads require authentication
    When I request the default grocery list
    Then the grocery response is unauthorized
