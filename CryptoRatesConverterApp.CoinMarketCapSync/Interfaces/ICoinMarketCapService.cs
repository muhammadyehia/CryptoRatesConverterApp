namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;

public interface ICoinMarketCapService
{
    Task<decimal?> GetCryptoPriceInEur(string crypto, CancellationToken cancellationToken);
    Task<Dictionary<string, decimal>> GetExchangeRates(CancellationToken cancellationToken);
}