using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using KnabCryptoRatesConverterApp.ApiService.Settings;
using Microsoft.Extensions.Options;

namespace KnabCryptoRatesConverterApp.ApiService.Services;

public class CryptoRatesProcessingJob : BackgroundService
{
    private readonly ILogger<CryptoRatesProcessingJob> _logger;
    private readonly IDistributedCache _cache;
    private readonly IConnection _messageConnection;
    private readonly CoinMarketCapSettings _settings;
    private IModel? _messageChannel;
    private EventingBasicConsumer? _consumer;

    public CryptoRatesProcessingJob(
        ILogger<CryptoRatesProcessingJob> logger,
        IDistributedCache cache,
        IConnection messageConnection,
        IOptions<CoinMarketCapSettings> settings)
    {
        _logger = logger;
        _cache = cache;
        _messageConnection = messageConnection;
        _settings = settings.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_settings.QueueName))
        {
            throw new InvalidOperationException("Queue name is not configured. Please check CoinMarketCapSettings configuration.");
        }

        _messageChannel = _messageConnection.CreateModel();
        _messageChannel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _consumer = new EventingBasicConsumer(_messageChannel);
        _consumer.Received += ProcessMessageAsync;

        _messageChannel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: true,
            consumer: _consumer);

        return Task.CompletedTask;
    }

    private async void ProcessMessageAsync(object? sender, BasicDeliverEventArgs args)
    {
        try
        {
            var messageText = Encoding.UTF8.GetString(args.Body.ToArray());
            var message = JsonSerializer.Deserialize<CryptoRateMessage>(messageText);

            if (message != null)
            {
                _logger.LogInformation("Received rates for {CryptoSymbol} at {Timestamp}", 
                    message.CryptoSymbol, 
                    message.Timestamp);

                // Store in Redis cache with crypto symbol as key
                var cacheKey = $"crypto:{message.CryptoSymbol.ToUpperInvariant()}";
                var ratesJson = JsonSerializer.Serialize(message.Rates);

                await _cache.SetStringAsync(
                    cacheKey,
                    ratesJson,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });

                _logger.LogInformation("Updated cache for {CryptoSymbol} with rates: {Rates}",
                    message.CryptoSymbol,
                    string.Join(", ", message.Rates.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing crypto rates message");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        _consumer!.Received -= ProcessMessageAsync;
        _messageChannel?.Dispose();
    }

    private record CryptoRateMessage(
        string CryptoSymbol,
        Dictionary<string, decimal> Rates,
        DateTime Timestamp
    );
} 