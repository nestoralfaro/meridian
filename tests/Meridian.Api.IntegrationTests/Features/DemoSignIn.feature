Feature: Demo sign-in
  As a hiring manager evaluating the portal
  I want to sign in as a ready-made demo client with one click
  So that I can explore the product without creating an account

  # Acceptance criteria from issue #16 (API level). The browser journey is covered by Playwright.

  Background:
    Given the demo personas exist

  Scenario: One-click sign-in as a persona
    When I sign in as the demo persona "dana"
    Then I am signed in as "Dana Lee"

  Scenario: The session is a secure cookie, not a token in storage
    When I sign in as the demo persona "dana"
    Then the session cookie is HttpOnly, Secure and SameSite=Strict
    And the response body contains no access token

  Scenario: Demo personas cannot change their credentials
    Given I am signed in as the demo persona "dana"
    When I try to change the password
    Then the response status is 403
    And the problem detail reads "Demo accounts can't change their password"

  Scenario: Sign-in requires an antiforgery token
    When I sign in as the demo persona "dana" without an antiforgery token
    Then the response status is 400

  Scenario: Only the known personas can be used
    When I sign in as the demo persona "mallory"
    Then the response status is 404

  Scenario: Visitors who aren't signed in get 401, not a redirect
    When I request "/api/v1/auth/me"
    Then the response status is 401
