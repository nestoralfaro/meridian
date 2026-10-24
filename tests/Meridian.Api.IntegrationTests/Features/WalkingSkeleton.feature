Feature: Walking skeleton
  As a reviewer
  I want the deployed URL to serve the real Angular app backed by the real API
  So that I can see the end-to-end architecture working, not a placeholder

  # Acceptance criteria from issue #9. Browser-level rendering is covered by Playwright (#25);
  # these scenarios verify hosting, routing and health behaviour against the real API and a real PostgreSQL.

  Scenario: The SPA is served by the API host
    Given the Meridian app is running
    When I open "/"
    Then I receive the Meridian single-page app
    And "/api/v1/version" is served from the same origin

  Scenario: Deep links are handled by the SPA
    Given the Meridian app is running
    When I open "/dashboard"
    Then I receive the Meridian single-page app

  Scenario: Unknown API routes are not swallowed by the SPA
    Given the Meridian app is running
    When I request "/api/v1/does-not-exist"
    Then the response status is 404
    And the response is a problem details document

  Scenario: Liveness does not depend on the database
    Given the database is unreachable
    When I request "/health/live"
    Then the response status is 200

  Scenario: Readiness reports the database
    Given the database is reachable
    When I request "/health/ready"
    Then the response status is 200
    And the "database" check is reported as "Healthy"

  Scenario: Readiness fails when the database is down
    Given the database is unreachable
    When I request "/health/ready"
    Then the response status is 503
    And the "database" check is reported as "Unhealthy"

  Scenario: Version endpoint identifies the build
    Given the Meridian app is running
    When I request "/api/v1/version"
    Then the response contains the git commit SHA of the running build
