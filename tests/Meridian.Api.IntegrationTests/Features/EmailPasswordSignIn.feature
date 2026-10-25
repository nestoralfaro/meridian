Feature: Email and password sign-in
  As a new client
  I want to create an account and sign in with my email and password
  So that my information is protected and only I can access it

  # Acceptance criteria from issue #17 (API level), plus the security properties behind them.

  Background:
    Given the Meridian app is running with a fresh database

  Scenario: Successful registration
    When I register as "Avery Stone" with a unique email and the password "correct horse battery staple"
    Then my account is created
    And I am signed in as "Avery Stone"

  Scenario: Weak password is rejected with guidance
    When I register as "Avery Stone" with a unique email and the password "password1"
    Then the response status is 400
    And the "password" field reports "Passwords must be at least 12 characters."

  Scenario: Commonly used passwords are rejected even when long enough
    When I register as "Avery Stone" with a unique email and the password "password1234"
    Then the response status is 400
    And the "password" field reports "This password is too common. Choose something harder to guess."

  Scenario: Signing in with the right password
    Given an account exists for "raj@example.test" with the password "a long and unusual passphrase"
    When I sign in as "raj@example.test" with the password "a long and unusual passphrase"
    Then I am signed in as "Raj Example"

  Scenario: Repeated failures lock the account
    Given an account exists for "raj@example.test" with the password "a long and unusual passphrase"
    When 5 sign-in attempts for "raj@example.test" fail in a row
    And I sign in as "raj@example.test" with the password "a long and unusual passphrase"
    Then the response status is 401
    And the sign-in failure message does not reveal whether the email exists

  Scenario: Unknown emails get the same answer as wrong passwords
    Given an account exists for "raj@example.test" with the password "a long and unusual passphrase"
    When I sign in as "nobody@example.test" with the password "a long and unusual passphrase"
    Then the response status is 401
    And the sign-in failure message does not reveal whether the email exists

  Scenario: Sign-in is protected against cross-site request forgery
    When a sign-in request is sent without a valid antiforgery token
    Then the response status is 400

  Scenario: Demo personas can't be used with a password
    Given the demo personas exist
    When I sign in as "dana@meridian.test" with the password "anything at all here"
    Then the response status is 401
