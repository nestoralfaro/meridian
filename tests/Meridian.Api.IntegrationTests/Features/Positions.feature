Feature: Positions
  As a client
  I want to see the holdings in an account
  So that I know what I own and how each position is doing

  # Acceptance criteria from issue #62.

  Background:
    Given the demo personas exist

  Scenario: Holdings in an account
    Given I am signed in as the demo persona "dana"
    When I request the positions in Dana's "Individual Brokerage"
    Then the response status is 200
    And the positions are "MDTOT, ACME"
    And the positions' market values add up to the account's value less its cash

  Scenario: Sort and page on the server
    Given I am signed in as the demo persona "raj"
    When I request page 2 of Raj's "Individual Brokerage" positions, 2 per page, sorted by "marketValue" "desc"
    Then the positions are "ACME, GLBX"
    And there are 4 positions on 2 pages

  Scenario: Filter by symbol or name
    Given I am signed in as the demo persona "raj"
    When I request Raj's "Individual Brokerage" positions filtered by "index"
    Then the positions are "MDTOT, MDINT"

  Scenario: Unknown sort columns are rejected
    Given I am signed in as the demo persona "raj"
    When I request Raj's "Individual Brokerage" positions sorted by "costBasis; drop table"
    Then the response status is 400
    And the response is a problem details document

  Scenario: Another client's positions are not available
    Given I am signed in as the demo persona "dana"
    When I request the positions in Raj's "Individual Brokerage"
    Then the response status is 404
    And an "AccessDenied" audit event is recorded for Dana
