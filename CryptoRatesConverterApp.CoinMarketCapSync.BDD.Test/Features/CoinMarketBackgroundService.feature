Feature: CoinMarketBackgroundService Symbol Processing
    As a crypto service
    I want to process cryptocurrency symbols correctly
    So that I can provide accurate rate information

Scenario: Process single cryptocurrency symbol
    Given I have configured the service with symbol "BTC"
    When I process the cryptocurrency rates
    Then the service should request rates for "BTC"
    And the rates should be processed successfully

Scenario: Skip processing for invalid symbol
    Given I have configured the service with symbol ""
    When I process the cryptocurrency rates
    Then the service should not request any rates
    And an error should be logged for invalid symbol

Scenario: Handle rate fetch failure for symbol
    Given I have configured the service with symbol "ETH"
    And the rate fetch will fail for "ETH"
    When I process the cryptocurrency rates
    Then an error should be logged for "ETH"
    And the service should continue processing 