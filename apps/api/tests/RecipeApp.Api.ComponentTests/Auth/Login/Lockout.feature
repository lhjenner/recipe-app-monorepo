Feature: Account lockout
    As the system owner
    I want accounts to lock after repeated failed logins
    So that password guessing is slowed down

    Scenario: Four failed attempts do not lock the account
        Given a registered account with email "four_fails@example.com" and password "password123"
        When I submit 4 login requests with an invalid password
        And I submit a login request
        Then login is successful

    Scenario: Five consecutive failures lock the account and the next attempt is rejected
        Given a registered account with email "locked_user@example.com" and password "password123"
        When I submit 5 login requests with an invalid password
        And I submit a login request
        Then the response status is 423
        And the response is a lockout problem details response
        And the response tells the client to retry in 900 seconds

    Scenario: The lockout lasts 15 minutes
        Given a registered account with email "expiring_lock@example.com" and password "password123"
        When I submit 5 login requests with an invalid password
        And 14 minutes pass
        And I submit a login request
        Then the response status is 423
        When 1 minutes pass
        And I submit a login request
        Then login is successful

    Scenario: A locked account does not issue a session
        Given a registered account with email "no_session@example.com" and password "password123"
        When I submit 5 login requests with an invalid password
        And I submit a login request
        Then no session cookie is issued

    Scenario: A successful login resets the consecutive failure counter
        Given a registered account with email "reset_user@example.com" and password "password123"
        When I submit 3 login requests with an invalid password
        And I submit a login request
        Then login is successful
        And the stored failed login counter is 0
        When I submit 4 login requests with an invalid password
        And I submit a login request
        Then login is successful

    Scenario: Failures for an unregistered email never lock anything
        Given an unregistered email address "ghost@example.com" and password "password123"
        When I submit 6 login requests with an invalid password
        And I submit a login request
        Then the response informs of invalid credentials
