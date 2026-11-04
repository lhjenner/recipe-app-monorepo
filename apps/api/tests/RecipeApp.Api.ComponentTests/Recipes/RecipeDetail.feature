Feature: Recipe detail API
  As an authenticated user
  I want to view one of my recipes
  So that I can follow its ingredients and instructions

  Scenario: Recipe detail returns owned active recipe content in source order
    Given a logged in user
    And the user owns a recipe with ordered detail content
    When I request that recipe's detail
    Then the recipe detail contains ordered metadata, tags, steps, and typed items
    And the recipe detail exposes only safe application image metadata

  Scenario: Recipe detail has no image metadata when there is no image
    Given a logged in user
    And the user owns an active recipe named "No Image Recipe" without an image
    When I request that recipe's detail
    Then the recipe detail image is null

  Scenario: Recipe detail requires authentication
    When I request a recipe detail without signing in
    Then the recipe detail response is unauthorized

  Scenario: Missing, archived, and other-owner recipe details are indistinguishable
    Given a logged in user
    And there are active recipes owned by another user and an archived recipe owned by me
    When I request the inaccessible recipe details
    Then every recipe detail response is not found

  Scenario: Recipe detail does not accept a legacy ID
    Given a logged in user
    When I request recipe detail using the legacy ID "Legacy-Recipe-001"
    Then the recipe detail response is not found
