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
