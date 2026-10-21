Feature: Ingredient read APIs
  As an authenticated user
  I want to read my ingredients and their store mappings
  So that I can maintain an accurate ingredient catalogue

  Scenario: Ingredient reads require authentication
    When I request the ingredient list and a detail without signing in
    Then both ingredient responses are unauthorized

  Scenario: Ingredient list is paged and sorted by normalized name
    Given a logged in user
    And the user owns 27 active ingredients
    When I request the default ingredient list
    Then the ingredient list contains 25 items, 27 total, on page 1
    And the ingredient names are sorted by normalized name
    When I request ingredient page 2 with page size 25
    Then the ingredient list contains 2 items, 27 total, on page 2

  Scenario: Ingredient search is case-insensitive and server-side
    Given a logged in user
    And the user owns ingredients named "green Apple", "Apple Pie", and "Banana"
    When I search ingredients for "APPLE"
    Then the ingredient list contains the names "Apple Pie,green Apple"
    And the ingredient list total is 2

  Scenario: Ingredient reads do not expose another user's records
    Given a logged in user
    And the user owns an ingredient named "Private Ingredient"
    And another user owns an ingredient named "Other Ingredient"
    When another user signs in
    And I request the previous user's ingredient
    Then the response status is 404
    When I request ingredient page 1 with page size 25
    Then the ingredient list does not contain "Private Ingredient"
    And the ingredient list contains "Other Ingredient"

  Scenario: Archived filter returns only archived ingredients
    Given a logged in user
    And the user owns one active ingredient and one archived ingredient
    When I request archived ingredient page 1 with page size 25
    Then the ingredient list contains only archived items

  Scenario: Ingredient detail includes category, nutrition, links, and every store state
    Given a logged in user
    And the user owns an ingredient with category, nutrition, product link, and store mappings
    When I request that ingredient's detail
    Then the ingredient detail includes its category, nutrition, product link, and store states

  Scenario: Invalid ingredient paging parameters are rejected
    Given a logged in user
    And the user owns one active ingredient and no other ingredients
    When I request ingredient page 2 with page size 25
    Then the response is a 400 problem details response
    When I request ingredient page 1 with page size 0
    Then the response is a 400 problem details response
    When I request ingredient page 1 with page size 101
    Then the response is a 400 problem details response
