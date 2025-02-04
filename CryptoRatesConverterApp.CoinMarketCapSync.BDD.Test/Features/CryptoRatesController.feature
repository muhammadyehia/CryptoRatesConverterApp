Feature: CryptoRatesController
    As an API consumer
    I want to retrieve cryptocurrency rates
    So that I can display current rates in different currencies

Background:
    Given the crypto rates service is initialized
    And the logger is configured

Scenario: Successfully retrieve rates for a valid cryptocurrency
    Given I have a valid cryptocurrency symbol "BTC"
    When I request the rates for the stored cryptocurrency
    Then the response should be successful
    And the response should contain rates for different currencies

Scenario: Request rates for a non-existent cryptocurrency
    Given I have an invalid cryptocurrency symbol "INVALID"
    When I request the rates for the stored cryptocurrency
    Then the response should be not found
    And the response should contain an appropriate error message

Scenario: Handle service error gracefully
    Given the crypto rates service is experiencing an error
    When I request the rates for cryptocurrency "BTC"
    Then the response should be internal server error
    And the response should contain an error message
    And the error should be logged