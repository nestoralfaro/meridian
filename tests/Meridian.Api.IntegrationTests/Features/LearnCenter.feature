Feature: Learn center
  As a visitor
  I want short articles about investing basics
  So that I understand the terms the portal uses

  # Acceptance criteria from issue #66.

  Scenario: Browse articles
    Given the Meridian app is running
    When I request "/api/v1/learn/articles"
    Then the response status is 200
    And the articles include "What is asset allocation?" with a summary and reading time
    And the response may be cached publicly

  Scenario: Read an article
    Given the Meridian app is running
    When I request "/api/v1/learn/articles/asset-allocation"
    Then the response status is 200
    And the article's HTML starts with a level-2 heading

  Scenario: Drafts stay hidden
    Given the Meridian app is running
    When I request "/api/v1/learn/articles/fees-draft"
    Then the response status is 404
