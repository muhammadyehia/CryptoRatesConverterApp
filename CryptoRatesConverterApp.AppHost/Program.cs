using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Redis service
var cache = builder.AddRedis("cache").WithRedisCommander();

// Add RabbitMQ service
var messaging = builder.AddRabbitMQ("messaging");
 
// Add CoinMarketCap Sync Service
var coinMarketCapSync = builder.AddProject<Projects.CryptoRatesConverterApp_CoinMarketCapSync>("coinmarketcap-sync")
    .WithReference(messaging)
    .WithHttpEndpoint(5001)
    .WaitFor(messaging);

// Add API Service
var apiService = builder.AddProject<Projects.CryptoRatesConverterApp_ApiService>("api-service")
    .WithReference(cache)
    .WithReference(messaging)
    .WithReference(coinMarketCapSync)
    .WaitFor(cache)
    .WaitFor(messaging)
    .WaitFor(coinMarketCapSync);

// Add Web Frontend
var webFrontend = builder.AddProject<Projects.CryptoRatesConverterApp_Web>("web-frontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
