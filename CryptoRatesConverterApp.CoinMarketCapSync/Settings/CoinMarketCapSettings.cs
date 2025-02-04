namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;

public class CoinMarketCapSettings
{
    public required string ApiKey { get; set; }
    public required string ExchangeRatesApiKey { get; set; }
    public required List<string> Cryptocurrencies { get; set; }
    public required List<string> Currencies { get; set; }
    public int UpdateIntervalSeconds { get; set; } = 60;
    public int ErrorRetryDelaySeconds { get; set; } 
    public int RateLimitDelayMs { get; set; } 
    public required string CoinMarketCapBaseUrl { get; set; }
    public required string CoinMarketCapEndpoint { get; set; }
    public required string ExchangeRatesBaseUrl { get; set; }
    public required string ExchangeRatesEndpoint { get; set; }
    public required string BaseCurrency { get; set; } 
    public required string DateTimeFormat { get; set; } 
    public int PriceDecimalPlaces { get; set; }
    public required string QueueName { get; set; }
} 