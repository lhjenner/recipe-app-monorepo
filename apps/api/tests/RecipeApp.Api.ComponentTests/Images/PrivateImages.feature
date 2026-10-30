Feature: Private recipe image delivery
  Synthetic objects and isolated PostgreSQL verify private image delivery without cloud credentials.

  Scenario: An owner reads their private image
    Given a signed-in synthetic image owner with an active recipe image
    When the recipe image is requested
    Then the exact synthetic bytes and private image headers are returned

  Scenario Outline: Inaccessible images never reach storage
    Given a signed-in synthetic image owner with an active recipe image
    And the recipe image is inaccessible because it is <reason>
    When the recipe image is requested
    Then the image response is <status> and storage has not been read
    Examples:
      | reason        | status |
      | anonymous     | 401    |
      | another owner | 404    |
      | archived      | 404    |
      | absent        | 404    |
      | missing       | 404    |

  Scenario: Conditional image requests still require authorization
    Given a signed-in synthetic image owner with an active recipe image
    When the recipe image is requested with its current ETag
    Then the image response is 304
    When the same conditional image request is made anonymously
    Then the image response is 401

  Scenario Outline: Storage failures are explicit
    Given a signed-in synthetic image owner with an active recipe image
    And synthetic image storage is <failure>
    When the recipe image is requested
    Then the image response is <status>
    And the image response is Problem Details
    Examples:
      | failure     | status |
      | unavailable | 503    |
      | missing     | 503    |
      | corrupt     | 503    |
