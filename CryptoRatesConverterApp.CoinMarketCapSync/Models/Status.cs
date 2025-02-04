using System.Text.Json.Serialization;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Models;

public class Status
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("error_code")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}