using CryptoRatesConverterApp.CoinMarketCapSync.Services;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Services;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using KnabCryptoRatesConverterApp.CoinMarketCapSync.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (includes basic OpenTelemetry)
builder.AddServiceDefaults();

// Add RabbitMQ client
builder.AddRabbitMQClient("messaging");

// Add controllers
builder.Services.AddControllers();

// Enhance OpenTelemetry with additional configuration
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
{
    metrics.AddMeter("CoinMarketCapSync.Metrics");
    metrics.AddView("crypto_price",
        new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new double[] { 1, 10, 100, 1000, 10000 }
        });
    metrics.AddView("exchange_rate", new ExplicitBucketHistogramConfiguration
    {
        Boundaries = new double[] { 0.1, 0.5, 1, 2, 5, 10 }
    });
});

builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
{
    tracing.AddSource("CoinMarketCapSync.Activities");
    tracing.SetSampler(new AlwaysOnSampler());
});

// Add configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Add services
builder.Services.Configure<CoinMarketCapSettings>(
    builder.Configuration.GetSection("CoinMarketCapSettings"));

// Configure HTTP clients with resilience
builder.Services.AddHttpClient("CoinMarketCap", (sp, client) =>
{
    var settings = sp.GetRequiredService<IConfiguration>()
        .GetSection("CoinMarketCapSettings").Get<CoinMarketCapSettings>();
    
    if (settings == null || string.IsNullOrEmpty(settings.CoinMarketCapBaseUrl))
    {
        throw new InvalidOperationException("CoinMarketCap base URL is not configured");
    }
    
    client.BaseAddress = new Uri(settings.CoinMarketCapBaseUrl);
    client.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", settings.ApiKey);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("ExchangeRates", (sp, client) =>
{
    var settings = sp.GetRequiredService<IConfiguration>()
        .GetSection("CoinMarketCapSettings").Get<CoinMarketCapSettings>();
    
    if (settings == null || string.IsNullOrEmpty(settings.ExchangeRatesBaseUrl))
    {
        throw new InvalidOperationException("Exchange Rates base URL is not configured");
    }
    
    client.BaseAddress = new Uri(settings.ExchangeRatesBaseUrl);
})
.AddStandardResilienceHandler();

// Register services
builder.Services.AddSingleton<ICryptoRatesService, CryptoRatesService>();
builder.Services.AddHostedService<CoinMarketBackgroundService>();

// Add health checks with more detailed configuration
builder.Services.AddHealthChecks()
    .AddCheck<CoinMarketCapHealthCheck>(
        "coinmarketcap-api",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready", "live" },
        timeout: TimeSpan.FromSeconds(5)
    );

var app = builder.Build();

// Map default endpoints (includes health checks)
app.MapDefaultEndpoints();

// Map controllers
app.MapControllers();

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Application error: {ex.Message}");
    Environment.Exit(1);
}