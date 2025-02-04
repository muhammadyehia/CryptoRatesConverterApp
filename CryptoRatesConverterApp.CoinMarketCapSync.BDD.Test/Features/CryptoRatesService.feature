Feature: CryptoRatesService
    As a crypto rates service
    I want to fetch cryptocurrency prices and exchange rates
    So that I can provide accurate currency conversions

Scenario: Successfully fetch crypto price in EUR
    Given the CoinMarketCap API will return a successful response for BTC
    When I request the crypto price in EUR for BTC
    Then the price should be 50000.00

Scenario: Handle missing crypto data
    Given the CoinMarketCap API will return a not found response for UNKNOWN
    When I request the crypto price in EUR for UNKNOWN
    Then an exception should be thrown

Scenario: Successfully fetch exchange rates
    Given the exchange rates API will return a successful response
    When I request the exchange rates
    Then the exchange rates should contain correct values

Scenario: Handle exchange rates API error
    Given the exchange rates API will return an error response
    When I request the exchange rates
    Then an exception should be thrown

Scenario: Handle malformed CoinMarketCap API response
    Given the CoinMarketCap API will return a malformed response for BTC
    When I request the crypto price in EUR for BTC
    Then an exception should be thrown

Scenario: Handle CoinMarketCap API server error
    Given the CoinMarketCap API will return a server error for BTC
    When I request the crypto price in EUR for BTC
    Then an exception should be thrown

Scenario: Handle CoinMarketCap API rate limit exceeded
    Given the CoinMarketCap API will return a rate limit exceeded response for BTC
    When I request the crypto price in EUR for BTC
    Then an exception should be thrown

Scenario: Handle exchange rates API invalid API key
    Given the exchange rates API will return an invalid API key response
    When I request the exchange rates
    Then an exception should be thrown

Scenario: Handle exchange rates API missing currency
    Given the exchange rates API will return a response with missing currency
    When I request the exchange rates
    Then the exchange rates should contain partial values 