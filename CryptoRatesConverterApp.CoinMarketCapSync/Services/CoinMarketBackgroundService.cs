using System.Text;
using System.Text.Json;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace KnabCryptoRatesConverterApp.CoinMarketCapSync.Services;

public class CoinMarketBackgroundService : BackgroundService
{
    private readonly ICryptoRatesService _cryptoRatesService;
    private readonly CoinMarketCapSettings _settings;
    private readonly ILogger<CoinMarketBackgroundService> _logger;
    private readonly IConnection _messageConnection;
    private readonly IModel _messageChannel;

    public CoinMarketBackgroundService(
        ICryptoRatesService cryptoRatesService,
        IOptions<CoinMarketCapSettings> settings,
        ILogger<CoinMarketBackgroundService> logger,
        IConnection messageConnection)
    {
        _cryptoRatesService = cryptoRatesService;
        _settings = settings.Value;
        _logger = logger;
        _messageConnection = messageConnection;
        _messageChannel = _messageConnection.CreateModel();
        
        // Declare the queue
        _messageChannel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var crypto in _settings.Cryptocurrencies)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(crypto))
                        {
                            _logger.LogError("Invalid cryptocurrency symbol: empty or whitespace");
                            continue;
                        }

                        _logger.LogInformation("Processing rates for {Crypto}", crypto);
                        
                        var rates = await _cryptoRatesService.GetCryptoRates(crypto, stoppingToken);
                        if (rates != null)
                        {
                            // Create message
                            var message = new
                            {
                                CryptoSymbol = crypto,
                                Rates = rates,
                                Timestamp = DateTime.UtcNow
                            };

                            // Publish to queue
                            var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                            _messageChannel.BasicPublish(
                                exchange: "",
                                routingKey: _settings.QueueName,
                                basicProperties: null,
                                body: messageBody);
                            
                            _logger.LogInformation("Published rates for {Crypto}: {Rates}", 
                                crypto, 
                                string.Join(", ", rates.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
                        }
                        
                        // Add delay between cryptocurrencies to respect rate limits
                        if (crypto != _settings.Cryptocurrencies.Last())
                        {
                            await Task.Delay(_settings.RateLimitDelayMs, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing {Crypto}", crypto);
                        continue; // Continue with next cryptocurrency
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cryptocurrency price fetch cycle");
                await Task.Delay(TimeSpan.FromSeconds(_settings.ErrorRetryDelaySeconds), stoppingToken);
                continue;
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.UpdateIntervalSeconds), stoppingToken);
        }
    }

    public override void Dispose()
    {
        _messageChannel?.Dispose();
        _messageConnection?.Dispose();
        base.Dispose();
    }
} 