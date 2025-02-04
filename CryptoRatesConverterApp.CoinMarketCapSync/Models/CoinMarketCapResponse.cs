using System.Text.Json.Serialization;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Models;

public class CoinMarketCapResponse
{
    [JsonPropertyName("data")]
    public List<CryptoCurrency> Data { get; set; } = new();

    [JsonPropertyName("status")]
    public Status Status { get; set; } = new();
}