using KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Services;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CryptoRatesController : ControllerBase
{
    private readonly ICryptoRatesService _cryptoRatesService;
    private readonly ILogger<CryptoRatesController> _logger;

    public CryptoRatesController(ICryptoRatesService cryptoRatesService, ILogger<CryptoRatesController> logger)
    {
        _cryptoRatesService = cryptoRatesService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the rates for a specific cryptocurrency in all configured currencies
    /// </summary>
    /// <param name="symbol">The cryptocurrency symbol (e.g., BTC, ETH)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary with currency codes as keys and rates as values</returns>
    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(Dictionary<string, decimal>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRates(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting rates for {Symbol}", symbol);
            var rates = await _cryptoRatesService.GetCryptoRates(symbol.ToUpperInvariant(), cancellationToken);
            
            if (rates == null)
            {
                return NotFound($"No rates found for cryptocurrency: {symbol}");
            }
            
            return Ok(rates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rates for {Symbol}", symbol);
            return StatusCode(500, "An error occurred while fetching cryptocurrency rates");
        }
    }
} 