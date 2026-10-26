Feature: Ingredient create and edit APIs
  As an authenticated user
  I want to create and update my ingredients
  So that ingredient data remains complete and safe from stale edits

  Scenario: Create a valid ingredient
    Given a logged in user
    When I create an ingredient named "Added Ingredient"
    Then the ingredient is created with a version 7 ID and a location

  Scenario: Reject invalid nutrition values
    Given a logged in user
    When I create an ingredient with negative energy
    Then the response is a 400 problem details response

  Scenario: Reject a duplicate normalized active name
    Given a logged in user
    And the user owns an ingredient named "Green  Apple"
    When I create an ingredient named " green apple "
    Then the response reports an ingredient name conflict

  Scenario: Reject a name held by an archived ingredient with restore guidance
    Given a logged in user
    And the user owns an archived ingredient named "Archived Apple"
    When I create an ingredient named " archived apple "
    Then the response identifies the archived ingredient and offers restore guidance

  Scenario: Child-only edits advance the aggregate version
    Given a logged in user
    And the user owns an ingredient with category, nutrition, product link, and store mappings
    When I update only a store mapping
    Then the ingredient version advances and the new mapping is saved
