using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using KnabCryptoRatesConverterApp.ApiService.Services;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using KnabCryptoRatesConverterApp.ApiService.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire integrations
builder.AddServiceDefaults();
builder.AddRedisDistributedCache("cache");

// Add services to the container
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.AddRabbitMQClient("messaging");
builder.Services.AddHostedService<CryptoRatesProcessingJob>();

// Configure HTTP client for CoinMarketCap Sync service
builder.Services.AddHttpClient("CryptoRatesApi")
    .ConfigureHttpClient(client => 
    {
        client.BaseAddress = new Uri("http://coinmarketcap-sync");
    })
    .AddStandardResilienceHandler();

builder.Services.Configure<CoinMarketCapSettings>(
    builder.Configuration.GetSection("CoinMarketCapSettings"));

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
   //
}

// Crypto rates endpoint
app.MapGet("/api/cryptorates/{symbol}", async (
    string symbol,
    IDistributedCache cache,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    try
    {
        symbol = symbol.ToUpperInvariant();
        logger.LogInformation("Getting rates for {Symbol}", symbol);

        // Try to get from cache first
        var cacheKey = $"crypto:{symbol}";
        var cachedRates = await cache.GetStringAsync(cacheKey);
        
        if (!string.IsNullOrEmpty(cachedRates))
        {
            logger.LogInformation("Cache hit for {Symbol}", symbol);
            return Results.Text(cachedRates, "application/json");
        }

        logger.LogInformation("Cache miss for {Symbol}, fetching from API", symbol);
        
        // Call the CryptoRates API
        var client = httpClientFactory.CreateClient("CryptoRatesApi");
        var response = await client.GetAsync($"api/CryptoRates/{symbol}");
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.Text($"{{\"error\": \"No rates found for cryptocurrency: {symbol}\"}}", "application/json", statusCode: 404);
            }
            
            throw new HttpRequestException($"Error fetching rates: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();

        // Cache the result
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        await cache.SetStringAsync(cacheKey, content, cacheOptions);

        return Results.Text(content, "application/json");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting rates for {Symbol}", symbol);
        return Results.Text($"{{\"error\": \"An error occurred while fetching cryptocurrency rates\"}}", "application/json", statusCode: 500);
    }
});

app.MapDefaultEndpoints();

app.Run();




