namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Models;

public class CryptoRate
{
    public required string CryptoSymbol { get; set; }
    public required Dictionary<string, decimal> Rates { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
} 