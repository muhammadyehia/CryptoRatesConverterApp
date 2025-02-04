namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;

public interface ICryptoRatesService
{
    /// <summary>
    /// Gets the cryptocurrency rates in EUR and converts them to other specified currencies
    /// </summary>
    /// <param name="cryptoSymbol">The cryptocurrency symbol (e.g., BTC, ETH)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary with currency codes as keys and rates as values</returns>
    Task<Dictionary<string, decimal>?> GetCryptoRates(string cryptoSymbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cryptocurrency price in EUR
    /// </summary>
    /// <param name="cryptoSymbol">The cryptocurrency symbol (e.g., BTC, ETH)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Price in EUR or null if not found</returns>
    Task<decimal?> GetCryptoPriceInEur(string cryptoSymbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets exchange rates for EUR to other currencies
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary with currency codes as keys and exchange rates as values</returns>
    Task<Dictionary<string, decimal>> GetExchangeRates(CancellationToken cancellationToken = default);
} 