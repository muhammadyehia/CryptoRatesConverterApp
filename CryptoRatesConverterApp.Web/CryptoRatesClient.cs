using System.Net.Http.Json;

namespace CryptoRatesConverterApp.Web;

public class CryptoRatesClient
{
    private readonly HttpClient _client;

    public CryptoRatesClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<Dictionary<string, decimal>> GetRatesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetFromJsonAsync<Dictionary<string, decimal>>($"api/cryptorates/{symbol}", cancellationToken);
        return response ?? new Dictionary<string, decimal>();
    }
} 