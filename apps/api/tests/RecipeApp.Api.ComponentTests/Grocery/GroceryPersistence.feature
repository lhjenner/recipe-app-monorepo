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
