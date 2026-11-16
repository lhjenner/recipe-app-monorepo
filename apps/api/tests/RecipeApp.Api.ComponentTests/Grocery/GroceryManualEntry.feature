Feature: Atomic manual grocery entry
  As an authenticated user
  I want to add manual grocery items safely
  So that duplicate demand and selected Ingredient creation remain attributable and atomic

  Scenario: Add an ad-hoc item and replay the command
    Given a logged in user
    When I submit one manual item "Green apples" with quantity "2" "piece"
    Then the grocery API creates one manual line named "Green apples" with quantity "2"
    When I replay the same grocery command
    Then the grocery API returns the same command without a second increment

  Scenario: Review and confirm an increment to an existing line
    Given a logged in user
    When I submit one manual item "Kale" with quantity "1" "bag"
    And I submit a second manual increment for "Kale" with quantity "2" "bag"
    Then the grocery API requests duplicate review without changing the line
    When I confirm the duplicate review
    Then the grocery API creates one manual line named "Kale" with quantity "3"

  Scenario: Consolidate duplicate rows in one batch only after review
    Given a logged in user
    When I submit duplicate manual rows for "Cumin"
    Then the grocery API requests a review of the pending batch rows
    When I confirm the duplicate review
    Then the grocery API creates one manual line named "Cumin" with quantity "3"

  Scenario: Save a selected new row as an Ingredient in the same command
    Given a logged in user
    When I submit one manual item "Ground coriander" with quantity "1" "jar" and save it as an Ingredient
    Then the grocery API creates one linked Ingredient line named "Ground coriander"

  Scenario: Manual quantity edits cannot reduce outstanding Recipe demand
    Given a logged in user
    And the owner has a measured Recipe requirement and manual grocery demand
    When I try to reduce the grocery line below its Recipe demand
    Then the grocery API rejects the reduction and preserves the outstanding quantity

  Scenario: Edit shopping details without changing the grocery line identity
    Given a logged in user
    When I submit one manual item "Cocoa" with quantity "2" "tin"
    And I edit manual grocery item "Cocoa" to quantity "3" "tin" and note "For baking"
    Then the grocery API updates the same manual line to quantity "3" and note "For baking"

  Scenario: Cancel an item and re-add it as a new outstanding line
    Given a logged in user
    When I submit one manual item "Paprika" with quantity "1" "tin"
    And I cancel the manual grocery item named "Paprika"
    Then the cancelled grocery line is retained but absent from the default list
    When I submit one manual item "Paprika" with quantity "1" "tin"
    Then the re-added grocery line has a new identity

  Scenario: Approve a selected-store department change on the linked Ingredient
    Given a logged in user
    And the owner has a linked grocery item and a new department
    When I update the linked Ingredient department for the seeded store
    Then the grocery API updates the selected-store master department

  Scenario: New Ingredient name collisions require explicit lookup selection
    Given a logged in user
    And the owner has an active Ingredient named "Coriander"
    When I submit one manual item "Coriander" with quantity "1" "jar" and save it as an Ingredient
    Then the grocery API requires an existing Ingredient selection without partial writes
