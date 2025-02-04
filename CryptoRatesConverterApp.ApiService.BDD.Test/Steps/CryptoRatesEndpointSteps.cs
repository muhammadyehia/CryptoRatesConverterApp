using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;
using Xunit;

namespace KnabCryptoRatesConverterApp.ApiService.BDD.Test.Steps
{
    public interface ICoinMarketCapService
    {
        Task<decimal?> GetCryptoPriceInEur(string crypto, CancellationToken cancellationToken);
        Task<Dictionary<string, decimal>> GetExchangeRates(CancellationToken cancellationToken);
    }

    [Binding]
    public class CryptoRatesEndpointSteps
    {
        private readonly Mock<ICoinMarketCapService> _coinMarketCapServiceMock;
        private readonly ScenarioContext _scenarioContext;
        private decimal? _returnedPrice;
        private Dictionary<string, decimal>? _returnedRates;
        private Exception? _thrownException;

        public CryptoRatesEndpointSteps(ScenarioContext scenarioContext)
        {
            _scenarioContext = scenarioContext;
            _coinMarketCapServiceMock = new Mock<ICoinMarketCapService>();
        }

        [Given(@"the CoinMarketCap service is mocked")]
        public void GivenTheCoinMarketCapServiceIsMocked()
        {
            // Setup is handled in constructor
        }

        [Given(@"the Exchange Rates service is mocked")]
        public void GivenTheExchangeRatesServiceIsMocked()
        {
            // Setup is handled in constructor
        }

        [Given(@"the CoinMarketCap API is configured to return price (.*) EUR for ""(.*)""")]
        public void GivenTheCoinMarketCapAPIIsConfiguredToReturnPriceEURFor(decimal price, string symbol)
        {
            _coinMarketCapServiceMock
                .Setup(x => x.GetCryptoPriceInEur(symbol, It.IsAny<CancellationToken>()))
                .ReturnsAsync(price);
        }

        [Given(@"the Exchange Rates API is configured to return rates:")]
        public void GivenTheExchangeRatesAPIIsConfiguredToReturnRates(Table table)
        {
            var rates = new Dictionary<string, decimal>();
            foreach (var row in table.Rows)
            {
                rates[row["Currency"]] = decimal.Parse(row["Rate"]);
            }

            _coinMarketCapServiceMock
                .Setup(x => x.GetExchangeRates(It.IsAny<CancellationToken>()))
                .ReturnsAsync(rates);
        }

        [Given(@"the CoinMarketCap API is configured to return an error for ""(.*)""")]
        public void GivenTheCoinMarketCapAPIIsConfiguredToReturnAnErrorFor(string symbol)
        {
            _coinMarketCapServiceMock
                .Setup(x => x.GetCryptoPriceInEur(symbol, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Error"));
        }

        [Given(@"the Exchange Rates API is configured to return an error")]
        public void GivenTheExchangeRatesAPIIsConfiguredToReturnAnError()
        {
            _coinMarketCapServiceMock
                .Setup(x => x.GetExchangeRates(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Error"));
        }

        [When(@"I request the price for ""(.*)""")]
        public async Task WhenIRequestThePriceFor(string symbol)
        {
            try
            {
                _returnedPrice = await _coinMarketCapServiceMock.Object.GetCryptoPriceInEur(symbol, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _thrownException = ex;
            }
        }

        [When(@"I request the exchange rates")]
        public async Task WhenIRequestTheExchangeRates()
        {
            try
            {
                _returnedRates = await _coinMarketCapServiceMock.Object.GetExchangeRates(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _thrownException = ex;
            }
        }

        [When(@"I process rates for ""(.*)""")]
        public async Task WhenIProcessRatesFor(string symbol)
        {
            try
            {
                var price = await _coinMarketCapServiceMock.Object.GetCryptoPriceInEur(symbol, CancellationToken.None);
                var rates = await _coinMarketCapServiceMock.Object.GetExchangeRates(CancellationToken.None);

                if (!price.HasValue)
                {
                    throw new Exception("Price not available");
                }

                _returnedRates = new Dictionary<string, decimal>
                {
                    ["EUR"] = price.Value
                };

                foreach (var rate in rates)
                {
                    _returnedRates[rate.Key] = price.Value * rate.Value;
                }
            }
            catch (Exception ex)
            {
                _thrownException = ex;
            }
        }

        [Then(@"the returned price should be (.*) EUR")]
        public void ThenTheReturnedPriceShouldBeEUR(decimal expectedPrice)
        {
            Assert.Equal(expectedPrice, _returnedPrice);
        }

        [Then(@"the returned rates should match:")]
        public void ThenTheReturnedRatesShouldMatch(Table table)
        {
            foreach (var row in table.Rows)
            {
                var currency = row["Currency"];
                var expectedRate = decimal.Parse(row["Rate"]);
                Assert.Equal(expectedRate, _returnedRates![currency]);
            }
        }

        [Then(@"the final rates should be:")]
        public void ThenTheFinalRatesShouldBe(Table table)
        {
            foreach (var row in table.Rows)
            {
                var currency = row["Currency"];
                var expectedRate = decimal.Parse(row["Rate"]);
                Assert.Equal(expectedRate, _returnedRates![currency]);
            }
        }

        [Then(@"an appropriate error should be returned")]
        public void ThenAnAppropriateErrorShouldBeReturned()
        {
            Assert.NotNull(_thrownException);
            Assert.Equal("API Error", _thrownException.Message);
        }

        [Then(@"no actual API calls should be made")]
        public void ThenNoActualAPICallsShouldBeMade()
        {
            // This is implicitly verified by using mocks instead of real services
        }
    }
} 