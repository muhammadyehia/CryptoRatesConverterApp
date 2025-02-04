using FluentAssertions;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Controllers;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TechTalk.SpecFlow;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.BDD.Test.Steps;

[Binding]
public class CryptoRatesControllerSteps
{
    private readonly Mock<ICryptoRatesService> _cryptoRatesServiceMock;
    private readonly Mock<ILogger<CryptoRatesController>> _loggerMock;
    private readonly CryptoRatesController _controller;
    private IActionResult _response;
    private string _cryptoSymbol;

    public CryptoRatesControllerSteps()
    {
        _cryptoRatesServiceMock = new Mock<ICryptoRatesService>();
        _loggerMock = new Mock<ILogger<CryptoRatesController>>();
        _controller = new CryptoRatesController(_cryptoRatesServiceMock.Object, _loggerMock.Object);
    }

    [Given(@"the crypto rates service is initialized")]
    public void GivenTheCryptoRatesServiceIsInitialized()
    {
        _cryptoRatesServiceMock.Reset();
        _cryptoRatesServiceMock
            .Setup(x => x.GetCryptoRates(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<string, decimal>)null);
    }

    [Given(@"the logger is configured")]
    public void GivenTheLoggerIsConfigured()
    {
        _loggerMock.Reset();
    }

    [Given(@"I have a valid cryptocurrency symbol ""(.*)""")]
    public void GivenIHaveAValidCryptocurrencySymbol(string symbol)
    {
        _cryptoSymbol = symbol.ToUpperInvariant();
        var rates = new Dictionary<string, decimal>
        {
            { "USD", 50000.00m },
            { "EUR", 42000.00m },
            { "GBP", 35000.00m }
        };
        
        _cryptoRatesServiceMock
            .Setup(x => x.GetCryptoRates(_cryptoSymbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rates);
    }

    [Given(@"I have an invalid cryptocurrency symbol ""(.*)""")]
    public void GivenIHaveAnInvalidCryptocurrencySymbol(string symbol)
    {
        _cryptoSymbol = symbol.ToUpperInvariant();
        _cryptoRatesServiceMock
            .Setup(x => x.GetCryptoRates(_cryptoSymbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<string, decimal>)null);
    }

    [Given(@"the crypto rates service is experiencing an error")]
    public void GivenTheCryptoRatesServiceIsExperiencingAnError()
    {
        _cryptoRatesServiceMock
            .Setup(x => x.GetCryptoRates(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));
    }

    [When(@"I request the rates for the stored cryptocurrency")]
    public async Task WhenIRequestTheRatesForTheStoredCryptocurrency()
    {
        _response = await _controller.GetRates(_cryptoSymbol, CancellationToken.None);
    }

    [When(@"I request the rates for cryptocurrency ""(.*)""")]
    public async Task WhenIRequestTheRatesForCryptocurrency(string symbol)
    {
        _response = await _controller.GetRates(symbol.ToUpperInvariant(), CancellationToken.None);
    }

    [Then(@"the response should be successful")]
    public void ThenTheResponseShouldBeSuccessful()
    {
        _response.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)_response).StatusCode.Should().Be(200);
    }

    [Then(@"the response should contain rates for different currencies")]
    public void ThenTheResponseShouldContainRatesForDifferentCurrencies()
    {
        var result = ((OkObjectResult)_response).Value as Dictionary<string, decimal>;
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().ContainKeys("USD", "EUR", "GBP");
        result["USD"].Should().Be(50000.00m);
        result["EUR"].Should().Be(42000.00m);
        result["GBP"].Should().Be(35000.00m);
    }

    [Then(@"the response should be not found")]
    public void ThenTheResponseShouldBeNotFound()
    {
        _response.Should().BeOfType<NotFoundObjectResult>();
        ((NotFoundObjectResult)_response).StatusCode.Should().Be(404);
    }

    [Then(@"the response should be internal server error")]
    public void ThenTheResponseShouldBeInternalServerError()
    {
        _response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)_response).StatusCode.Should().Be(500);
    }

    [Then(@"the response should contain an(?:\s*appropriate)? error message")]
    public void ThenTheResponseShouldContainAnErrorMessage()
    {
        var result = (_response as ObjectResult)?.Value as string;
        result.Should().NotBeNullOrEmpty();
    }

    [Then(@"the error should be logged")]
    public void ThenTheErrorShouldBeLogged()
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
} 