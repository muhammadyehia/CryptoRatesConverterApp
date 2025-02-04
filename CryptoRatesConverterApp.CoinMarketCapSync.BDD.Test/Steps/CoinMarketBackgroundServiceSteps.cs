using System.Text;
using System.Text.Json;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Services;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using TechTalk.SpecFlow;
using Xunit;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.BDD.Test.Steps;

[Binding]
public class CoinMarketBackgroundServiceSteps : IDisposable
{
    private readonly Mock<ICryptoRatesService> _cryptoRatesService;
    private readonly Mock<ILogger<CoinMarketBackgroundService>> _logger;
    private readonly Mock<IConnection> _connection;
    private readonly Mock<IModel> _channel;
    private CoinMarketBackgroundService _service;
    private CoinMarketCapSettings _settings;
    private CancellationTokenSource _cts;
    private Exception _thrownException;

    public CoinMarketBackgroundServiceSteps()
    {
        _cryptoRatesService = new Mock<ICryptoRatesService>();
        _logger = new Mock<ILogger<CoinMarketBackgroundService>>();
        _connection = new Mock<IConnection>();
        _channel = new Mock<IModel>();
        _cts = new CancellationTokenSource();

        _connection.Setup(x => x.CreateModel()).Returns(_channel.Object);
        _channel.Setup(x => x.QueueDeclare(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>()
        ));
    }

    [Given(@"I have configured the service with symbol ""(.*)""")]
    public void GivenIHaveConfiguredTheServiceWithSymbol(string symbol)
    {
        _settings = new CoinMarketCapSettings
        {
            Cryptocurrencies = new List<string> { symbol },
            QueueName = "test-queue",
            UpdateIntervalSeconds = 1,
            ErrorRetryDelaySeconds = 1,
            RateLimitDelayMs = 100,
            ApiKey = "test-api-key",
            ExchangeRatesApiKey = "test-exchange-rates-key",
            Currencies = new List<string> { "USD", "EUR" },
            CoinMarketCapBaseUrl = "https://test.coinmarketcap.com",
            CoinMarketCapEndpoint = "/v1/test",
            ExchangeRatesBaseUrl = "https://test.exchangerates.com",
            ExchangeRatesEndpoint = "/v1/test",
            BaseCurrency = "USD",
            DateTimeFormat = "yyyy-MM-dd HH:mm:ss"
        };

        _service = new CoinMarketBackgroundService(
            _cryptoRatesService.Object,
            Options.Create(_settings),
            _logger.Object,
            _connection.Object
        );

        // Setup default success case
        _cryptoRatesService
            .Setup(x => x.GetCryptoRates(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { { "USD", 50000m } });
    }

    [Given(@"the rate fetch will fail for ""(.*)""")]
    public void GivenTheRateFetchWillFailFor(string symbol)
    {
        _cryptoRatesService
            .Setup(x => x.GetCryptoRates(symbol, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception($"Failed to fetch rates for {symbol}"));
    }

    [When(@"I process the cryptocurrency rates")]
    public async Task WhenIProcessTheCryptocurrencyRates()
    {
        try
        {
            await _service.StartAsync(_cts.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(500)); // Allow time for one processing cycle
            await _service.StopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [Then(@"the service should request rates for ""(.*)""")]
    public void ThenTheServiceShouldRequestRatesFor(string symbol)
    {
        _cryptoRatesService.Verify(
            x => x.GetCryptoRates(symbol, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Then(@"the rates should be processed successfully")]
    public void ThenTheRatesShouldBeProcessedSuccessfully()
    {
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Never);
    }

    [Then(@"an error should be logged for ""(.*)""")]
    public void ThenAnErrorShouldBeLoggedFor(string symbol)
    {
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(symbol)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Then(@"the service should continue processing")]
    public void ThenTheServiceShouldContinueProcessing()
    {
        Assert.Null(_thrownException);
    }

    [Then(@"the service should not request any rates")]
    public void ThenTheServiceShouldNotRequestAnyRates()
    {
        _cryptoRatesService.Verify(
            x => x.GetCryptoRates(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Then(@"an error should be logged for invalid symbol")]
    public void ThenAnErrorShouldBeLoggedForInvalidSymbol()
    {
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalid cryptocurrency symbol")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _service?.Dispose();
    }
} 