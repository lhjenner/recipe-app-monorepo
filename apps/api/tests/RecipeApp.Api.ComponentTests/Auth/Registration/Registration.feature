Feature: Account registration
  As a user
  I want to register with an email and password
  So that I can access the app

  Scenario: Register a new email
    Given the email "new@example.com" is available for registration
    When I submit registration with password "password123"
    Then the response status is 201
    And the response contains the user profile for "new@example.com"
    And the stored password is a BCrypt hash for "password123"

  Scenario: Reject an email that is already registered
    Given the email "existing@example.com" is already registered
    When I submit registration with password "password123"
    Then the response status is 409
    And the response is an RFC 7807 problem details response

  Scenario: Reject malformed email and empty password
    When I submit registration with email "not-an-email" and password ""
    Then the response status is 400
    And the response contains validation errors for "Email" and "Password"
