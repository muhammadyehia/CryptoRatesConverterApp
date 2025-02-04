using System.Text.Json;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CryptoRatesConverterApp.CoinMarketCapSync.Services;

public class CoinMarketCapHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CoinMarketCapSettings _settings;
    private readonly ILogger<CoinMarketCapHealthCheck> _logger;

    public CoinMarketCapHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<CoinMarketCapSettings> settings,
        ILogger<CoinMarketCapHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var isHealthy = true;
        var description = new List<string>();
        
        try
        {
            // Check CoinMarketCap API
            var coinMarketCapUrl = $"{_settings.CoinMarketCapBaseUrl}?symbol={_settings.Cryptocurrencies.First()}&convert={_settings.BaseCurrency}";
            using var cmcClient = _httpClientFactory.CreateClient("CoinMarketCap");
            using var cmcResponse = await cmcClient.GetAsync(coinMarketCapUrl, cancellationToken);
            
            if (!cmcResponse.IsSuccessStatusCode)
            {
                isHealthy = false;
                var cmcErrorStatus = $"CoinMarketCap API Error: {cmcResponse.StatusCode}";
                data["CoinMarketCapStatus"] = cmcErrorStatus;
                description.Add(cmcErrorStatus);
            }
            else
            {
                var content = await cmcResponse.Content.ReadAsStringAsync(cancellationToken);
                var jsonData = JsonSerializer.Deserialize<JsonElement>(content);
                
                if (!jsonData.TryGetProperty("status", out var cmcStatusData) ||
                    !cmcStatusData.TryGetProperty("error_code", out var errorCode) ||
                    errorCode.GetInt32() != 0)
                {
                    isHealthy = false;
                    var cmcErrorStatus = "CoinMarketCap API returned error status";
                    data["CoinMarketCapStatus"] = cmcErrorStatus;
                    description.Add(cmcErrorStatus);
                }
                else
                {
                    data["CoinMarketCapStatus"] = "Healthy";
                }
            }

            // Check Exchange Rates API
            var currencies = _settings.Currencies.Where(c => c != _settings.BaseCurrency).Take(1);
            var exchangeRatesUrl = $"{_settings.ExchangeRatesBaseUrl}?access_key={_settings.ExchangeRatesApiKey}&base={_settings.BaseCurrency}&symbols={string.Join(",", currencies)}";
            using var erClient = _httpClientFactory.CreateClient();
            using var erResponse = await erClient.GetAsync(exchangeRatesUrl, cancellationToken);

            if (!erResponse.IsSuccessStatusCode)
            {
                isHealthy = false;
                var erErrorStatus = $"Exchange Rates API Error: {erResponse.StatusCode}";
                data["ExchangeRatesStatus"] = erErrorStatus;
                description.Add(erErrorStatus);
            }
            else
            {
                var content = await erResponse.Content.ReadAsStringAsync(cancellationToken);
                var jsonData = JsonSerializer.Deserialize<JsonElement>(content);
                
                if (!jsonData.TryGetProperty("success", out var erSuccess) || !erSuccess.GetBoolean())
                {
                    isHealthy = false;
                    var erErrorStatus = "Exchange Rates API returned error status";
                    data["ExchangeRatesStatus"] = erErrorStatus;
                    description.Add(erErrorStatus);
                }
                else
                {
                    data["ExchangeRatesStatus"] = "Healthy";
                }
            }

            var healthStatus = isHealthy ? HealthStatus.Healthy : context.Registration.FailureStatus;
            var message = isHealthy ? "All APIs are operational" : string.Join(", ", description);

            return new HealthCheckResult(
                healthStatus,
                message,
                data: data
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Health check failed",
                ex,
                data
            );
        }
    }
} 

