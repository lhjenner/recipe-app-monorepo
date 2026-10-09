Feature: Session validity
    As a user
    I want my session to stay valid while I am active and expire when I am not
    So that my account stays secure without constant re-login

    Scenario: Profile request without a session is unauthorized
        When I request my current profile
        Then the request is unauthorized

    Scenario: Profile request with an invalid session cookie is unauthorized
        When I request my current profile with the session cookie "RecipeApp.Session=not-a-real-session"
        Then the request is unauthorized

    Scenario: Session is unauthorized after 14 days of inactivity
        Given a registered account with email "idle_user@example.com" and password "password123"
        When I submit a login request
        Then login is successful
        When 14 days and 1 minute pass
        And I request my current profile
        Then the request is unauthorized

    Scenario: Session is still valid just before 14 days of inactivity
        Given a registered account with email "active_user@example.com" and password "password123"
        When I submit a login request
        Then login is successful
        When 13 days pass
        And I request my current profile
        Then the current profile is returned for "active_user@example.com"

    Scenario: Activity renews the session using sliding expiration
        Given a registered account with email "sliding_user@example.com" and password "password123"
        When I submit a login request
        Then login is successful
        When 8 days pass
        And I request my current profile
        Then the current profile is returned for "sliding_user@example.com"
        When 8 days pass
        And I request my current profile
        Then the current profile is returned for "sliding_user@example.com"
        When 15 days pass
        And I request my current profile
        Then the request is unauthorized
