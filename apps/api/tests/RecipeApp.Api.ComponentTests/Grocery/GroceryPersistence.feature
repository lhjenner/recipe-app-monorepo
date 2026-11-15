Feature: Grocery persistence foundation

  Scenario: Concurrent first commands share one persistent list
    Given an owner without a grocery list
    When the owner reads the list without creating it
    Then the read is empty and no list is persisted
    When the owner issues concurrent first-list mutations
    Then both mutations use the same persistent grocery list

  Scenario: Grocery items cannot reference another owner's Ingredient
    Given two owners and a grocery list for the first owner
    When the first owner's grocery item references the second owner's Ingredient
    Then the cross-owner grocery link is rejected

  Scenario: Purchase history is append-only and reversible only once
    Given a purchase event for an owner exists
    When an exact reversal is recorded
    Then the original purchase and reversal are both retained
    When the purchase is reversed a second time
    Then the duplicate reversal is rejected
    When the original purchase event is edited
    Then the purchase event mutation is rejected

  Scenario: Long recipe snapshots and unresolved unit text remain persistable
    Given an owner with a long-named recipe and grocery list
    When an unresolved recipe contribution with a long unit is persisted
    Then the full recipe name and unit are retained with a bounded key

  Scenario: Undo keeps different purchase units on separate active lines
    Given a completed pack purchase and an active grams line with the same requirement
    When the pack purchase is undone
    Then both purchase-unit lines remain outstanding and the reversal is retained
