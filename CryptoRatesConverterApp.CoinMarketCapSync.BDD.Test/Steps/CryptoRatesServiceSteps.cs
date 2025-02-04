using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TechTalk.SpecFlow;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Services;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Xunit;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.BDD.Test.Steps;

[Binding]
public class CryptoRatesServiceSteps : IDisposable
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<CryptoRatesService>> _loggerMock;
    private readonly CoinMarketCapSettings _settings;
    private readonly CryptoRatesService _service;
    private decimal? _returnedPrice;
    private Dictionary<string, decimal>? _returnedRates;
    private Exception? _thrownException;

    public CryptoRatesServiceSteps()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<CryptoRatesService>>();
        
        _settings = new CoinMarketCapSettings
        {
            BaseCurrency = "EUR",
            Currencies = new List<string> { "EUR", "USD", "GBP" },
            Cryptocurrencies = new List<string> { "BTC", "ETH", "XRP" },
            QueueName = "crypto-rates",
            ApiKey = "test-api-key",
            ExchangeRatesApiKey = "test-exchange-key",
            CoinMarketCapBaseUrl = "https://pro-api.coinmarketcap.com",
            CoinMarketCapEndpoint = "/v1/cryptocurrency/quotes/latest",
            ExchangeRatesBaseUrl = "http://api.exchangeratesapi.io",
            ExchangeRatesEndpoint = "/v1/latest",
            DateTimeFormat = "yyyy-MM-dd HH:mm:ss",
            PriceDecimalPlaces = 2
        };

        var settingsOptions = Options.Create(_settings);
        _service = new CryptoRatesService(_httpClientFactoryMock.Object, settingsOptions, _loggerMock.Object);
    }

    [Given(@"the CoinMarketCap API will return a successful response for (.*)")]
    public void GivenTheCoinMarketCapAPIWillReturnASuccessfulResponseFor(string symbol)
    {
        var response = new
        {
            status = new { error_code = 0, error_message = (string)null },
            data = new Dictionary<string, object>
            {
                {
                    symbol, new
                    {
                        id = 1,
                        name = "Bitcoin",
                        symbol = symbol,
                        quote = new Dictionary<string, object>
                        {
                            {
                                "EUR", new
                                {
                                    price = 50000.00m,
                                    last_updated = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")
                                }
                            }
                        }
                    }
                }
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains($"symbol={symbol}", req.RequestUri!.Query);
            Assert.Contains("convert=EUR", req.RequestUri.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.CoinMarketCapBaseUrl)
        };
        httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _settings.ApiKey);

        _httpClientFactoryMock.Setup(x => x.CreateClient("CoinMarketCap")).Returns(httpClient);
    }

    [Given(@"the CoinMarketCap API will return a not found response for (.*)")]
    public void GivenTheCoinMarketCapAPIWillReturnANotFoundResponseFor(string symbol)
    {
        var response = new
        {
            status = new
            {
                error_code = 404,
                error_message = $"Symbol {symbol} not found"
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains($"symbol={symbol}", req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.CoinMarketCapBaseUrl)
        };
        httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _settings.ApiKey);

        _httpClientFactoryMock.Setup(x => x.CreateClient("CoinMarketCap")).Returns(httpClient);
    }

    [Given(@"the exchange rates API will return a successful response")]
    public void GivenTheExchangeRatesAPIWillReturnASuccessfulResponse()
    {
        var response = new
        {
            success = true,
            timestamp = 1631234567,
            base_currency = "EUR",
            rates = new Dictionary<string, decimal>
            {
                { "USD", 1.1m },
                { "GBP", 0.85m }
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("access_key=" + _settings.ExchangeRatesApiKey, req.RequestUri!.Query);
            Assert.Contains("base=EUR", req.RequestUri.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.ExchangeRatesBaseUrl)
        };

        _httpClientFactoryMock.Setup(x => x.CreateClient("ExchangeRates")).Returns(httpClient);
    }

    [Given(@"the exchange rates API will return an error response")]
    public void GivenTheExchangeRatesAPIWillReturnAnErrorResponse()
    {
        var response = new
        {
            success = false,
            error = new
            {
                code = 500,
                type = "internal_error",
                info = "An internal error occurred"
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("access_key=" + _settings.ExchangeRatesApiKey, req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.ExchangeRatesBaseUrl)
        };

        _httpClientFactoryMock.Setup(x => x.CreateClient("ExchangeRates")).Returns(httpClient);
    }

    [Given(@"the CoinMarketCap API will return a malformed response for (.*)")]
    public void GivenTheCoinMarketCapAPIWillReturnAMalformedResponseFor(string symbol)
    {
        // Invalid JSON format to trigger parsing error
        var jsonResponse = @"{
            ""status"": {""error_code"": 0, ""error_message"": null},
            ""data"": {
                """ + symbol + @""": {
                    ""id"": 1,
                    ""name"": ""Bitcoin"",
                    ""symbol"": """ + symbol + @""",
                    ""quote"": {
                        ""EUR"": {
                            ""price"": ""not_a_number"",  // Invalid price format
                            ""last_updated"": ""invalid_date""  // Invalid date format
                        }
                    }
                }
            }
        }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains($"symbol={symbol}", req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.CoinMarketCapBaseUrl)
        };
        httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _settings.ApiKey);

        _httpClientFactoryMock.Setup(x => x.CreateClient("CoinMarketCap")).Returns(httpClient);
    }

    [Given(@"the CoinMarketCap API will return a server error for (.*)")]
    public void GivenTheCoinMarketCapAPIWillReturnAServerErrorFor(string symbol)
    {
        var response = new
        {
            status = new
            {
                error_code = 500,
                error_message = "Internal Server Error"
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains($"symbol={symbol}", req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.CoinMarketCapBaseUrl)
        };
        httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _settings.ApiKey);

        _httpClientFactoryMock.Setup(x => x.CreateClient("CoinMarketCap")).Returns(httpClient);
    }

    [Given(@"the CoinMarketCap API will return a rate limit exceeded response for (.*)")]
    public void GivenTheCoinMarketCapAPIWillReturnARateLimitExceededResponseFor(string symbol)
    {
        var response = new
        {
            status = new
            {
                error_code = 429,
                error_message = "Too many requests. Rate limit exceeded."
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains($"symbol={symbol}", req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.CoinMarketCapBaseUrl)
        };
        httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _settings.ApiKey);

        _httpClientFactoryMock.Setup(x => x.CreateClient("CoinMarketCap")).Returns(httpClient);
    }

    [Given(@"the exchange rates API will return an invalid API key response")]
    public void GivenTheExchangeRatesAPIWillReturnAnInvalidAPIKeyResponse()
    {
        var response = new
        {
            success = false,
            error = new
            {
                code = 101,
                type = "invalid_access_key",
                info = "You have not supplied a valid API Access Key."
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("access_key=" + _settings.ExchangeRatesApiKey, req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.ExchangeRatesBaseUrl)
        };

        _httpClientFactoryMock.Setup(x => x.CreateClient("ExchangeRates")).Returns(httpClient);
    }

    [Given(@"the exchange rates API will return a response with missing currency")]
    public void GivenTheExchangeRatesAPIWillReturnAResponseWithMissingCurrency()
    {
        var response = new
        {
            success = true,
            timestamp = 1631234567,
            base_currency = "EUR",
            rates = new Dictionary<string, decimal>
            {
                { "USD", 1.1m }
                // GBP is missing
            }
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new TestMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Contains("access_key=" + _settings.ExchangeRatesApiKey, req.RequestUri!.Query);
            return httpResponse;
        }))
        {
            BaseAddress = new Uri(_settings.ExchangeRatesBaseUrl)
        };

        _httpClientFactoryMock.Setup(x => x.CreateClient("ExchangeRates")).Returns(httpClient);
    }

    [When(@"I request the crypto price in EUR for (.*)")]
    public async Task WhenIRequestTheCryptoPriceInEURFor(string symbol)
    {
        try
        {
            _returnedPrice = await _service.GetCryptoPriceInEur(symbol);
            _thrownException = null;
        }
        catch (Exception ex)
        {
            _thrownException = ex;
            _returnedPrice = null;
        }
    }

    [When(@"I request the exchange rates")]
    public async Task WhenIRequestTheExchangeRates()
    {
        try
        {
            _returnedRates = await _service.GetExchangeRates();
            _thrownException = null;
        }
        catch (Exception ex)
        {
            _thrownException = ex;
            _returnedRates = null;
        }
    }

    [Then(@"the price should be (.*)")]
    public void ThenThePriceShouldBe(decimal expectedPrice)
    {
        Assert.Null(_thrownException);
        Assert.NotNull(_returnedPrice);
        Assert.Equal(expectedPrice, _returnedPrice.Value);
    }

    [Then(@"the exchange rates should contain correct values")]
    public void ThenTheExchangeRatesShouldContainCorrectValues()
    {
        Assert.Null(_thrownException);
        Assert.NotNull(_returnedRates);
        Assert.Equal(1.1m, _returnedRates["USD"]);
        Assert.Equal(0.85m, _returnedRates["GBP"]);
    }

    [Then(@"an exception should be thrown")]
    public void ThenAnExceptionShouldBeThrown()
    {
        Assert.NotNull(_thrownException);
    }

    [Then(@"the exchange rates should contain partial values")]
    public void ThenTheExchangeRatesShouldContainPartialValues()
    {
        Assert.Null(_thrownException);
        Assert.NotNull(_returnedRates);
        Assert.Equal(1.1m, _returnedRates["USD"]);
        Assert.False(_returnedRates.ContainsKey("GBP"));
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}

public class TestMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public TestMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
} 