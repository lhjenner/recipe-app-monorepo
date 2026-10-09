Feature: Account Login
    As a user
    I want to login with my credentials
    So that I can access the app

    Scenario: Login with valid credentials
        Given a registered account with email "registered_user@example.com" and password "password123"
        When I submit a login request
        Then login is successful
        And the session cookie is HttpOnly, Secure, and SameSite
        When I request my current profile
        Then the current profile is returned for "registered_user@example.com"

    Scenario: Login with invalid email address
        Given an unregistered email address "unregistered_user@example.com" and password "password123"
        When I submit a login request
        Then the response informs of invalid credentials

    Scenario: Login with invalid password
        Given a registered account with email "registered_user@example.com" and password "password123"
        When I submit a login request with an invalid password
        Then the response informs of invalid credentials

    Scenario: User can log out and the old session is revoked
        Given a registered account with email "logout_user@example.com" and password "password123"
        When I submit a login request
        Then login is successful
        And the session cookie is HttpOnly, Secure, and SameSite
        When I log out
        Then logout is successful
        And the session cookie is cleared
        When I retry the original session cookie
        Then the request is unauthorized
        
    Scenario Outline: Reject malformed login requests with validation problem details
        When I submit a login request with email "<email>" and password "<password>"
        Then the response status is 400
        And the response contains a validation error for "<field>"

        Examples:
            | email             | password    | field    |
            | not-an-email      | password123 | Email    |
            |                   | password123 | Email    |
            | user@example.com  |             | Password |

    Scenario: Reject a login request with no body fields
        When I submit a login request with an empty JSON body
        Then the response status is 400
        And the response contains validation errors for "Email" and "Password"
