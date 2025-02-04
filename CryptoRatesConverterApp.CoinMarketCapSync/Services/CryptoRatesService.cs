using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Microsoft.Extensions.Options;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Services;

public class CryptoRatesService : ICryptoRatesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CoinMarketCapSettings _settings;
    private readonly ILogger<CryptoRatesService> _logger;
    private static readonly ActivitySource ActivitySource = new("CoinMarketCapSync.Activities");
    private static readonly Meter Meter = new("CoinMarketCapSync.Metrics");
    private static readonly Counter<int> ApiCallsCounter = Meter.CreateCounter<int>("api_calls_total", "Number of API calls made");
    private static readonly Histogram<double> CryptoPriceHistogram = Meter.CreateHistogram<double>("crypto_price", "Distribution of cryptocurrency prices");

    public CryptoRatesService(
        IHttpClientFactory httpClientFactory,
        IOptions<CoinMarketCapSettings> settings,
        ILogger<CryptoRatesService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>?> GetCryptoRates(string cryptoSymbol, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetCryptoRates", ActivityKind.Internal);
        activity?.SetTag("cryptocurrency", cryptoSymbol);

        try
        {
            // Step 1: Get crypto price in EUR
            var eurPrice = await GetCryptoPriceInEur(cryptoSymbol, cancellationToken);
            if (!eurPrice.HasValue)
            {
                return null;
            }

            // Step 2: Get exchange rates and convert price
            var exchangeRates = await GetExchangeRates(cancellationToken);

            // Calculate all prices
            var prices = new Dictionary<string, decimal>
            {
                [_settings.BaseCurrency] = eurPrice.Value
            };

            foreach (var (currency, rate) in exchangeRates)
            {
                prices[currency] = Math.Round(eurPrice.Value * rate, _settings.PriceDecimalPlaces);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return prices;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error getting rates for {CryptoSymbol}", cryptoSymbol);
            throw;
        }
    }

    public async Task<decimal?> GetCryptoPriceInEur(string cryptoSymbol, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetCryptoPriceInEur", ActivityKind.Client);
        activity?.SetTag("cryptocurrency", cryptoSymbol);
        
        try
        {
            using var client = _httpClientFactory.CreateClient("CoinMarketCap");
            var url = $"{_settings.CoinMarketCapEndpoint}?symbol={cryptoSymbol}&convert={_settings.BaseCurrency}";
            ApiCallsCounter.Add(1);
            
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (data.TryGetProperty("data", out var cryptoData) &&
                cryptoData.TryGetProperty(cryptoSymbol, out var cryptoElement) &&
                cryptoElement.TryGetProperty("quote", out var quoteData) &&
                quoteData.TryGetProperty(_settings.BaseCurrency, out var eurData) &&
                eurData.TryGetProperty("price", out var priceElement))
            {
                var price = Math.Round(priceElement.GetDecimal(), _settings.PriceDecimalPlaces);
                CryptoPriceHistogram.Record(Convert.ToDouble(price));
                activity?.SetTag("price_eur", price);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return price;
            }
            
            _logger.LogWarning("Could not find price data for {Crypto}", cryptoSymbol);
            return null;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error fetching price for {Crypto} from CoinMarketCap", cryptoSymbol);
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetExchangeRates(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetExchangeRates", ActivityKind.Client);
        try
        {
            var currencies = _settings.Currencies.Where(c => c != _settings.BaseCurrency).ToList();
            var currenciesParam = string.Join(",", currencies);
            
            using var client = _httpClientFactory.CreateClient("ExchangeRates");
            var url = $"{_settings.ExchangeRatesEndpoint}?access_key={_settings.ExchangeRatesApiKey}&base={_settings.BaseCurrency}&symbols={currenciesParam}";
            ApiCallsCounter.Add(1);
            
            using var response = await client.GetAsync(url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Exchange Rates API Error: {content}");
            }

            var data = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (!data.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                if (data.TryGetProperty("error", out var error))
                {
                    var code = error.GetProperty("code").GetInt32();
                    var info = error.GetProperty("info").GetString();
                    throw new InvalidOperationException($"Exchange Rates API Error: {code} - {info}");
                }
                throw new InvalidOperationException("Exchange rates API request was not successful");
            }

            var exchangeRates = new Dictionary<string, decimal>();
            if (data.TryGetProperty("rates", out var rates))
            {
                foreach (var currency in currencies)
                {
                    if (rates.TryGetProperty(currency, out var rate))
                    {
                        exchangeRates[currency] = rate.GetDecimal();
                        activity?.SetTag($"rate_eur_to_{currency}", rate.GetDecimal());
                    }
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return exchangeRates;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error fetching exchange rates");
            throw;
        }
    }
} 