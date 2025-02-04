using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using KnabCryptoRatesConverterApp.ApiService.Services;
using KnabCryptoRatesConverterApp.ApiService.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace KnabCryptoRatesConverterApp.ApiService.BDD.Test.Steps
{
    [Binding]
    public class CryptoRatesProcessingSteps
    {
        private readonly Mock<ILogger<CryptoRatesProcessingJob>> _loggerMock;
        private readonly Mock<IDistributedCache> _cacheMock;
        private readonly Mock<IConnection> _connectionMock;
        private readonly Mock<IModel> _channelMock;
        private readonly CryptoRatesProcessingJob _job;
        private string? _cryptoSymbol;
        private Dictionary<string, decimal>? _rates;
        private BasicDeliverEventArgs? _basicDeliverEventArgs;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CryptoRatesProcessingSteps()
        {
            _loggerMock = new Mock<ILogger<CryptoRatesProcessingJob>>();
            _cacheMock = new Mock<IDistributedCache>();
            _connectionMock = new Mock<IConnection>();
            _channelMock = new Mock<IModel>();
            _cancellationTokenSource = new CancellationTokenSource();

            var settings = new CoinMarketCapSettings { QueueName = "test_queue" };
            var optionsMock = new Mock<IOptions<CoinMarketCapSettings>>();
            optionsMock.Setup(x => x.Value).Returns(settings);

            _connectionMock.Setup(x => x.CreateModel()).Returns(_channelMock.Object);
            _job = new CryptoRatesProcessingJob(_loggerMock.Object, _cacheMock.Object, _connectionMock.Object, optionsMock.Object);
        }

        [Given(@"the message queue is available")]
        public void GivenTheMessageQueueIsAvailable()
        {
            _connectionMock.Setup(x => x.IsOpen).Returns(true);
            _channelMock.Setup(x => x.IsOpen).Returns(true);
        }

        [Given(@"the cache service is initialized")]
        public void GivenTheCacheServiceIsInitialized()
        {
            // Cache service is already initialized in constructor
        }

        [Given(@"a valid crypto rate message for ""(.*)"" with the following rates:")]
        public void GivenAValidCryptoRateMessageForWithTheFollowingRates(string cryptoSymbol, Table table)
        {
            _cryptoSymbol = cryptoSymbol;
            _rates = table.CreateSet<(string Currency, decimal Rate)>()
                .ToDictionary(x => x.Currency, x => x.Rate);

            var message = new
            {
                CryptoSymbol = cryptoSymbol,
                Rates = _rates,
                Timestamp = DateTime.UtcNow
            };
            var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            _basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                Body = messageBody
            };
        }

        [Given(@"an invalid message format is received")]
        public void GivenAnInvalidMessageFormatIsReceived()
        {
            var invalidJson = "{invalid_json}";
            var messageBody = Encoding.UTF8.GetBytes(invalidJson);
            _basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                Body = messageBody
            };
        }

        [Given(@"a crypto rate message for ""(.*)"" with empty rates")]
        public void GivenACryptoRateMessageForWithEmptyRates(string cryptoSymbol)
        {
            _cryptoSymbol = cryptoSymbol;
            _rates = new Dictionary<string, decimal>();

            var message = new
            {
                CryptoSymbol = cryptoSymbol,
                Rates = _rates,
                Timestamp = DateTime.UtcNow
            };
            var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            _basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                Body = messageBody
            };
        }

        [Given(@"a crypto rate message with null symbol")]
        public void GivenACryptoRateMessageWithNullSymbol()
        {
            var message = new
            {
                CryptoSymbol = (string?)null,
                Rates = new Dictionary<string, decimal> { { "USD", 50000 } },
                Timestamp = DateTime.UtcNow
            };
            var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            _basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                Body = messageBody
            };
        }

        [When(@"the message is processed")]
        public async Task WhenTheMessageIsProcessed()
        {
            var consumer = new EventingBasicConsumer(_channelMock.Object);
            
            _channelMock.Setup(x => x.BasicConsume(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IBasicConsumer>()))
            .Returns("test_consumer_tag")
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((queue, autoAck, consumerTag, noLocal, exclusive, arguments, eventingConsumer) =>
            {
                if (_basicDeliverEventArgs != null)
                {
                    var consumer = eventingConsumer as EventingBasicConsumer;
                    consumer!.HandleBasicDeliver(
                        consumerTag: "",
                        deliveryTag: 1,
                        redelivered: false,
                        exchange: "",
                        routingKey: "test_queue",
                        properties: null,
                        body: _basicDeliverEventArgs.Body
                    );
                }
            });

            await _job.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(100); // Give time for message processing
            _cancellationTokenSource.Cancel();
            await _job.StopAsync(_cancellationTokenSource.Token);
        }

        [Then(@"the rates should be cached successfully")]
        public void ThenTheRatesShouldBeCachedSuccessfully()
        {
            _cacheMock.Verify(x => x.SetAsync(
                It.Is<string>(key => key == $"crypto:{_cryptoSymbol}"),
                It.Is<byte[]>(bytes => VerifyRatesMatch(bytes, _rates!)),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Then(@"no errors should be logged")]
        public void ThenNoErrorsShouldBeLogged()
        {
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
        }

        [Then(@"an error should be logged with message ""(.*)""")]
        public void ThenAnErrorShouldBeLoggedWithMessage(string expectedMessage)
        {
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Then(@"no rates should be cached")]
        public void ThenNoRatesShouldBeCached()
        {
            _cacheMock.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Then(@"no rates should be cached for ""(.*)""")]
        public void ThenNoRatesShouldBeCachedForSymbol(string symbol)
        {
            _cacheMock.Verify(x => x.SetAsync(
                It.Is<string>(key => key == $"crypto:{symbol}"),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        private static bool VerifyRatesMatch(byte[] actualBytes, Dictionary<string, decimal> expectedRates)
        {
            try
            {
                var actualRates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(actualBytes);
                return actualRates != null && 
                       actualRates.Count == expectedRates.Count && 
                       actualRates.All(kvp => expectedRates.ContainsKey(kvp.Key) && expectedRates[kvp.Key] == kvp.Value);
            }
            catch
            {
                return false;
            }
        }
    }
} 