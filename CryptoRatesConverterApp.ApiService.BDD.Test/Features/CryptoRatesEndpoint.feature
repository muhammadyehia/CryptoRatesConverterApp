Feature: CryptoRatesEndpoint
    Testing the crypto rates service functionality with mocked external dependencies

    Background:
        Given the CoinMarketCap service is mocked
        And the Exchange Rates service is mocked

    Scenario: Successfully get crypto price in EUR
        Given the CoinMarketCap API is configured to return price 50000 EUR for "BTC"
        When I request the price for "BTC"
        Then the returned price should be 50000 EUR
        And no actual API calls should be made

    Scenario: Successfully get exchange rates
        Given the Exchange Rates API is configured to return rates:
            | Currency | Rate |
            | USD      | 1.1  |
            | GBP      | 0.85 |
        When I request the exchange rates
        Then the returned rates should match:
            | Currency | Rate |
            | USD      | 1.1  |
            | GBP      | 0.85 |
        And no actual API calls should be made

    Scenario: Handle CoinMarketCap API error
        Given the CoinMarketCap API is configured to return an error for "ETH"
        When I request the price for "ETH"
        Then an appropriate error should be returned
        And no actual API calls should be made

    Scenario: Handle Exchange Rates API error
        Given the Exchange Rates API is configured to return an error
        When I request the exchange rates
        Then an appropriate error should be returned
        And no actual API calls should be made

    Scenario: Process complete crypto rate conversion
        Given the CoinMarketCap API is configured to return price 50000 EUR for "BTC"
        And the Exchange Rates API is configured to return rates:
            | Currency | Rate |
            | USD      | 1.1  |
            | GBP      | 0.85 |
        When I process rates for "BTC"
        Then the final rates should be:
            | Currency | Rate    |
            | EUR      | 50000   |
            | USD      | 55000   |
            | GBP      | 42500   |
        And no actual API calls should be made 