Feature: Account ownership authorization
  As a client
  I want assurance that no one else can view my accounts
  So that I can trust Meridian with my money

  # Acceptance criteria from issue #19.

  Background:
    Given the demo personas exist

  Scenario: Accessing another client's account by ID is not possible
    Given I am signed in as the demo persona "dana"
    When I request Raj's "Traditional IRA" by its ID
    Then the response status is 404
    And an "AccessDenied" audit event is recorded for Dana

  Scenario: The answer doesn't reveal that the account exists
    Given I am signed in as the demo persona "dana"
    When I request Raj's "Traditional IRA" by its ID
    And I request an account ID that doesn't exist
    Then both answers are identical apart from the trace id

  Scenario: Clients can open their own accounts
    Given I am signed in as the demo persona "dana"
    When I request Dana's "Individual Brokerage" by its ID
    Then the response status is 200
    And the account is worth "12480.55"

  Scenario: Unauthenticated requests are rejected
    When I request "/api/v1/accounts"
    Then the response status is 401
