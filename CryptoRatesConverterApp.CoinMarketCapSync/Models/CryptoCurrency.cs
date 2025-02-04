using System.Text.Json.Serialization;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Models;

public class CryptoCurrency
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("quote")]
    public Dictionary<string, QuoteData> Quote { get; set; } = new();
}