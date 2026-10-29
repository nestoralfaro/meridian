Feature: Live quotes
  As a client
  I want prices on my account page to update while I watch
  So that I see current values without refreshing

  # Acceptance criteria from issue #65 (server side). The browser behaviour is covered by Playwright.

  Background:
    Given the demo personas exist

  Scenario: Watching my account streams values for my holdings
    Given Dana is connected to live quotes
    When she watches her "Individual Brokerage"
    Then she is told the account is being watched
    And she receives live values for "MDTOT, ACME" valued by the server
    And another update arrives within 10 seconds

  Scenario: Another client's account can't be watched
    Given Dana is connected to live quotes
    When she watches Raj's "Traditional IRA"
    Then she is told the account can't be watched
    And she receives no live values
    And an "AccessDenied" audit event is recorded for Dana

  Scenario: Anonymous visitors can't connect
    When an anonymous visitor connects to live quotes
    Then the connection is refused
