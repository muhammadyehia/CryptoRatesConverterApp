using System.Text.Json.Serialization;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Models;

public class QuoteData
{
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; }
}