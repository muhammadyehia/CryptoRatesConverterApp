using System.Net;
using System.Text;
using System.Text.Json;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using RabbitMQ.Client;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.BDD.Test.Helpers;

public static class TestHelper
{
    public static CoinMarketCapSettings CreateTestSettings()
    {
        return new CoinMarketCapSettings
        {
            BaseCurrency = "EUR",
            Currencies = new List<string> { "EUR", "USD", "GBP" },
            ApiKey = "test-api-key",
            ExchangeRatesApiKey = "test-exchange-key",
            Cryptocurrencies = new List<string> { "BTC", "ETH" },
            CoinMarketCapBaseUrl = "http://test.local",
            CoinMarketCapEndpoint = "/test/crypto",
            ExchangeRatesBaseUrl = "http://test.local",
            ExchangeRatesEndpoint = "/test/rates",
            DateTimeFormat = "yyyy-MM-dd HH:mm:ss",
            QueueName = "test-queue",
            PriceDecimalPlaces = 2,
            UpdateIntervalSeconds = 1,
            ErrorRetryDelaySeconds = 1,
            RateLimitDelayMs = 1
        };
    }

    public static Mock<IHttpClientFactory> CreateMockHttpClientFactory(Mock<HttpMessageHandler> handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        var client = new HttpClient(handler.Object);
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
        return factory;
    }

    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }

    public static (Mock<IConnection> connection, Mock<IModel> channel) CreateMockRabbitMQ()
    {
        var connection = new Mock<IConnection>();
        var channel = new Mock<IModel>();
        
        connection.Setup(x => x.CreateModel()).Returns(channel.Object);
        channel.Setup(x => x.QueueDeclare(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()));
        channel.Setup(x => x.BasicPublish(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()));

        return (connection, channel);
    }

    public static HttpResponseMessage CreateCryptoResponse(decimal price)
    {
        var response = new
        {
            status = new
            {
                timestamp = "2024-01-01T00:00:00.000Z",
                error_code = 0,
                error_message = (string)null,
                elapsed = 10,
                credit_count = 1
            },
            data = new Dictionary<string, object>
            {
                ["BTC"] = new
                {
                    id = 1,
                    name = "Bitcoin",
                    symbol = "BTC",
                    quote = new Dictionary<string, object>
                    {
                        ["EUR"] = new
                        {
                            price = price,
                            last_updated = "2024-01-01T00:00:00.000Z"
                        }
                    }
                }
            }
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(response),
                Encoding.UTF8,
                "application/json")
        };
    }

    public static HttpResponseMessage CreateExchangeRatesResponse(Dictionary<string, decimal> rates)
    {
        var response = new
        {
            success = true,
            timestamp = 1704067200,
            @base = "EUR",
            date = "2024-01-01",
            rates = rates
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(response),
                Encoding.UTF8,
                "application/json")
        };
    }

    public static HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, string message)
    {
        var response = new
        {
            status = new
            {
                error_code = (int)statusCode,
                error_message = message
            }
        };

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(response),
                Encoding.UTF8,
                "application/json")
        };
    }
} 