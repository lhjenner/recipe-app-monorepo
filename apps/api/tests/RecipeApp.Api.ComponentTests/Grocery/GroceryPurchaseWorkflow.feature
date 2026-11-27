Feature: Grocery purchases and history
  As an authenticated user
  I want whole-line purchases and reversible history
  So that shopping progress persists without losing purchase evidence

  Scenario: Purchase and undo a whole grocery line
    Given a logged in user
    When I add and purchase three synthetic tins
    Then the whole line is completed with one immutable purchase event
    When I undo the synthetic purchase
    Then the purchase remains with one reversal and exactly three tins outstanding
    When I undo the synthetic purchase again
    Then the purchase still has exactly one reversal

  Scenario: Undo keeps later demand on the outstanding line
    Given a logged in user
    When I add and purchase three synthetic tins
    Then the whole line is completed with one immutable purchase event
    When I add two more synthetic tins
    And I undo the synthetic purchase
    Then undo adds only the purchased quantity to the later outstanding line

  Scenario: Reject a partial purchase without writing history
    Given a logged in user
    When I try to purchase fewer than three synthetic tins
    Then the purchase is rejected without changing the line or history

  Scenario: Clear outstanding grocery demand without clearing history
    Given a logged in user
    When I clear two synthetic outstanding grocery lines
    Then both lines are cancelled and no purchase event is created

  Scenario: Clearing an uncreated list reserves the command ID
    Given a logged in user
    When I clear the empty list and later add an item
    Then reusing the clear command ID with a changed version is rejected

  Scenario: Purchase history snapshots the current ingredient and store names
    Given a logged in user
    When I purchase a linked item after renaming its ingredient
    And I rename its ingredient and store after purchase
    And I undo that purchase
    Then both history events retain the names from the time of purchase

  Scenario: Older purchase events use the current store name
    Given a logged in user
    And an older purchase event has a store ID but no store name snapshot
    When the older event's store is renamed
    Then its history shows the current store name

  Scenario: Recalculating measured recipes preserves the quantity restored by undo
    Given a logged in user
    And an active Recipe with measured flour demand to remove
    When I add this Recipe before adding a manual increment
    And I purchase the recipe line and add the recipe again
    And I undo that recipe purchase
    And I add the recipe once more
    Then the recipe line retains both purchased and later quantities
    When I remove the Recipe's outstanding requirements
    Then the quantity restored by undo still remains

  Scenario: Concurrent undo attempts never acknowledge an unreserved command
    Given a logged in user
    When I add and purchase three synthetic tins
    And I concurrently undo the purchase with different commands
    Then every successful undo command rejects reuse with changed content

  Scenario: Restored countable demand retains its Recipe floor until removal
    Given a logged in user
    And an active Recipe with countable onion demand to remove
    When I add this Recipe before adding a manual increment
    And I purchase the recipe line and add the recipe again
    And I undo that recipe purchase
    And I try to reduce the restored count below the Recipe floor
    Then the count reduction is rejected
    When I add the recipe once more
    Then the countable line contains six onions
    When I remove the Recipe's outstanding requirements
    Then two restored onions remain and can be reduced
