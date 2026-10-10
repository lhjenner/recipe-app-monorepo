Feature: Password reset
    As a user who forgot my password
    I want to reset it through an emailed link
    So that I can regain access to my account

    Scenario: Forgot-password responds identically for registered and unregistered emails
        Given a registered account with email "known@example.com" and password "password123"
        When I request a password reset for "known@example.com"
        Then the response status is 200
        When I request a password reset for "nobody@example.com"
        Then the response status is 200

    Scenario: A malformed email is rejected
        When I request a password reset for "not-an-email"
        Then the response status is 400

    Scenario: A reset email is sent only to registered addresses
        Given a registered account with email "known@example.com" and password "password123"
        When I request a password reset for "nobody@example.com"
        Then no reset email is sent
        When I request a password reset for "known@example.com"
        Then a reset email with a link is sent to "known@example.com"

    Scenario: A delivery failure does not change the response
        Given a registered account with email "known@example.com" and password "password123"
        And email delivery is failing
        When I request a password reset for "known@example.com"
        Then the response status is 200

    Scenario: The stored token is hashed
        Given a registered account with email "known@example.com" and password "password123"
        When I request a password reset for "known@example.com"
        Then the raw reset token is not stored in the database

    Scenario: A valid token resets the password
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        When I reset the password to "brand-new-pass"
        Then the response status is 204
        When I submit a login request with email "known@example.com" and password "brand-new-pass"
        Then login is successful

    Scenario: The old password stops working after a reset
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        When I reset the password to "brand-new-pass"
        And I submit a login request with email "known@example.com" and password "password123"
        Then the response status is 401

    Scenario: An unknown token is rejected
        When I reset the password using the token "not-a-real-token"
        Then the response status is 400
        And the response is an invalid reset token problem details response

    Scenario: A token expires after one hour
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        When 61 minutes pass
        And I reset the password to "brand-new-pass"
        Then the response status is 400

    Scenario: A token is still valid just before one hour
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        When 59 minutes pass
        And I reset the password to "brand-new-pass"
        Then the response status is 204

    Scenario: A token cannot be reused
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        When I reset the password to "first-new-pass"
        And I reset the password to "second-new-pass"
        Then the response status is 400

    Scenario: A newer request invalidates the earlier token
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        And I have requested a password reset for "known@example.com"
        When I reset the password using the first emailed token
        Then the response status is 400

    Scenario: A reset clears an account lockout
        Given a registered account with email "known@example.com" and password "password123"
        And I have requested a password reset for "known@example.com"
        When I submit 5 login requests with an invalid password
        And I reset the password to "brand-new-pass"
        And I submit a login request with email "known@example.com" and password "brand-new-pass"
        Then login is successful
