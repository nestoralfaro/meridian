Feature: Transfer between own accounts
  As a client
  I want to move cash between my own accounts
  So that I can fund my Roth IRA without calling anyone

  # Acceptance criteria from issue #63.

  Background:
    Given the demo personas exist
    And I am signed in as the demo persona "dana"

  Scenario: A confirmed transfer moves cash and is recorded everywhere
    When I transfer "500.00" from "Individual Brokerage" to "Roth IRA" with idempotency key "k-1"
    Then the response status is 201
    And "Individual Brokerage" has "881.05" in cash
    And "Roth IRA" has "689.20" in cash
    And "Individual Brokerage" activity shows "Transfer to Roth IRA" for "-500.00"
    And "Roth IRA" activity shows "Transfer from Individual Brokerage" for "500.00"
    And a "TransferCompleted" audit event is recorded

  Scenario: Retrying the same request does not move money twice
    When I transfer "500.00" from "Individual Brokerage" to "Roth IRA" with idempotency key "k-2"
    And I send exactly the same request again
    Then the response status is 200
    And the response says it was replayed
    And "Individual Brokerage" has "881.05" in cash

  Scenario: Concurrent retries with one key move money once
    When 5 identical transfers of "100.00" with idempotency key "k-race" arrive at the same time
    Then every response is 201 or 200 and they all name the same transfer
    And "Individual Brokerage" has "1281.05" in cash

  Scenario: Concurrent different transfers never overdraw
    When 4 transfers of "400.00" from "Individual Brokerage" with different keys arrive at the same time
    Then no more than 3 succeed
    And "Individual Brokerage" never has less than "0.00" in cash

  Scenario: Reusing a key for a different transfer is refused
    When I transfer "500.00" from "Individual Brokerage" to "Roth IRA" with idempotency key "k-3"
    And I transfer "600.00" from "Individual Brokerage" to "Roth IRA" with idempotency key "k-3"
    Then the response status is 422

  Scenario: Insufficient cash
    When I transfer "1381.06" from "Individual Brokerage" to "Roth IRA" with idempotency key "k-4"
    Then the response status is 422
    And the problem says "$1,381.05" is available
    And "Individual Brokerage" has "1381.05" in cash

  Scenario: A stale review is a conflict
    Given another transfer changed "Individual Brokerage" after I reviewed it
    When I confirm my reviewed transfer of "10.00" from "Individual Brokerage" to "Roth IRA"
    Then the response status is 409

  Scenario: Accounts I don't own
    When I transfer "1.00" from "Individual Brokerage" to Raj's "High-Yield Savings" with idempotency key "k-5"
    Then the response status is 404
    And an "AccessDenied" audit event is recorded for Dana

  Scenario: Requests without an idempotency key are rejected
    When I transfer "1.00" from "Individual Brokerage" to "Roth IRA" without an idempotency key
    Then the response status is 400

  Scenario: Requests without an antiforgery token are rejected
    When I transfer "1.00" from "Individual Brokerage" to "Roth IRA" without an antiforgery token
    Then the response status is 400
    And "Individual Brokerage" has "1381.05" in cash
