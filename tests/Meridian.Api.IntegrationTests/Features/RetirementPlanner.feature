Feature: Retirement planner
  As a client
  I want to see how likely my savings are to last through retirement
  So that I can decide whether to save more

  # Acceptance criteria from issue #64. The model itself is unit-tested in the domain.

  Background:
    Given the demo personas exist
    And I am signed in as the demo persona "raj"

  Scenario: Probability of success with percentile bands
    When I run a plan with 250000 saved, 1000 a month, age 52 retiring at 65, spending 40000 a year, 60% stocks
    Then the response status is 200
    And the success rate is between 0 and 1
    And there is a projection for every age from 52 to 95

  Scenario: Reproducible results
    When I run the same plan twice
    Then both results are identical

  Scenario: Invalid inputs
    When I run a plan with 250000 saved, -5 a month, age 52 retiring at 65, spending 40000 a year, 60% stocks
    Then the response status is 400
    And the problem names the field "monthlyContribution"
