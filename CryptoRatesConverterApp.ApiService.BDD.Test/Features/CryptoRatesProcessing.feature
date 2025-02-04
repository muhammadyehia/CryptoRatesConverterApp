Feature: CryptoRatesProcessingJob
    Processing crypto rate messages from the queue to maintain up-to-date rates in the cache.

    Scenario: Successfully process a valid crypto rate message
        Given the message queue is available
        And the cache service is initialized
        And a valid crypto rate message for "BTC" with the following rates:
            | Currency | Rate |
            | USD     | 50000|
            | EUR     | 45000|
        When the message is processed
        Then the rates should be cached successfully
        And no errors should be logged

    Scenario: Handle invalid message format
        Given the message queue is available
        And the cache service is initialized
        And an invalid message format is received
        When the message is processed
        Then an error should be logged with message "Error processing crypto rates message"
        And no rates should be cached

    Scenario: Process message with empty rates
        Given the message queue is available
        And the cache service is initialized
        And a crypto rate message for "ETH" with empty rates
        When the message is processed
        Then the rates should be cached successfully
        And no errors should be logged

    Scenario: Process message with null crypto symbol
        Given the message queue is available
        And the cache service is initialized
        And a crypto rate message with null symbol
        When the message is processed
        Then an error should be logged with message "Error processing crypto rates message"
        And no rates should be cached 