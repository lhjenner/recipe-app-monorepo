Feature: Ingredient lookup endpoints
  As an authenticated user
  I want ingredient lookups and supermarket reference data
  So that ingredient maintenance can use consistent owned and shared values

  Scenario: Every lookup endpoint requires authentication
    When I request every lookup endpoint without signing in
    Then every lookup response is unauthorized

  Scenario: Supermarkets return the seeded chains and stores
    Given a logged in user
    When I request the supermarkets
    Then the response status is 200
    And the response contains all seeded supermarket chains and stores

  Scenario: Categories and departments are sorted and private to their owner
    Given a logged in user
    When I create a category named "Zucchini"
    And I create a category named "Apples"
    And I create a department named "Produce"
    And I create a department named "Baking"
    And I request the categories
    Then the category names are sorted as "Apples,Zucchini"
    When I request the departments
    Then the department names are sorted as "Baking,Produce"
    When another user signs in
    And I request the categories
    Then the response contains no categories
    When I request the departments
    Then the response contains no departments

  Scenario: Category creation is idempotent after name normalization
    Given a logged in user
    When I create a category named "  Dry    Goods  "
    Then the response status is 200
    When I create a category named "dry goods"
    Then the response status is 200
    And the response is the same category as before

  Scenario: Department creation is idempotent after name normalization
    Given a logged in user
    When I create a department named "  Baking    Aisle  "
    Then the response status is 200
    When I create a department named "baking aisle"
    Then the response status is 200
    And the response is the same department as before

  Scenario: A blank category name is rejected
    Given a logged in user
    When I create a category named "   "
    Then the response status is 400

  Scenario: A category name over 100 characters is rejected
    Given a logged in user
    When I create a category with a name longer than 100 characters
    Then the response status is 400

  Scenario: A blank department name is rejected
    Given a logged in user
    When I create a department named "   "
    Then the response status is 400

  Scenario: A department name over 100 characters is rejected
    Given a logged in user
    When I create a department with a name longer than 100 characters
    Then the response status is 400
