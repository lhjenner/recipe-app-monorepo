Feature: Recipe list API
  As an authenticated user
  I want to list and search my recipes
  So that I can find a recipe to view

  Scenario: Recipe list requires authentication
    When I request the recipe list without signing in
    Then the recipe list response is unauthorized

  Scenario: Recipe list is paged in normalized-name order
    Given a logged in user
    And the user owns 27 active recipes
    When I request the default recipe list
    Then the recipe list contains 25 items, 27 total, on page 1
    And the recipe names are sorted by normalized name
    When I request recipe page 2 with page size 25
    Then the recipe list contains 2 items, 27 total, on page 2

  Scenario: Recipe search trims input and treats wildcard characters literally
    Given a logged in user
    And the user owns recipes:
      | name           |
      | 100% Tofu      |
      | 100X Tofu      |
      | Tofu_ Bowl     |
      | TofuX Bowl     |
      | Back\slash Tofu|
      | unrelated      |
    When I search recipes for " TOFU "
    Then the recipe list contains the names "100% Tofu,100X Tofu,Back\slash Tofu,Tofu_ Bowl,TofuX Bowl"
    When I search recipes for "%"
    Then the recipe list contains the names "100% Tofu"
    When I search recipes for "_"
    Then the recipe list contains the names "Tofu_ Bowl"
    When I search recipes for a backslash
    Then the recipe list contains the names "Back\slash Tofu"

  Scenario: Recipe search matches current linked ingredient names once
    Given a logged in user
    And an archived and grocery-excluded ingredient named "Tofu" is linked twice to "Miso Bowl"
    And a recipe named "Cabbage Soup" has a heading item named "Tofu"
    And the user owns one active recipe named "Tofu Soup"
    When I search recipes for "TOFU"
    Then the recipe list contains the names "Miso Bowl,Tofu Soup"

  Scenario: Recipe list includes only the current owner's active recipes
    Given a logged in user
    And the user owns one active recipe named "My Recipe"
    And the user owns one archived recipe named "Archived Recipe"
    And another user owns an active recipe named "Other Recipe"
    When I request the default recipe list
    Then the recipe list contains the names "My Recipe"

  Scenario: Invalid and out-of-range recipe list inputs are rejected
    Given a logged in user
    And the user owns one active recipe named "Only Recipe"
    When I request recipe page 0 with page size 25
    Then the response is a 400 problem details response
    When I request recipe page 1 with page size 101
    Then the response is a 400 problem details response
    When I search recipes for a 201 character query
    Then the response is a 400 problem details response
    When I request recipe page 2 with page size 25
    Then the response is a 400 problem details response
