# KnabCryptoRatesConverterApp

A modern, distributed application for real-time cryptocurrency rate conversion built with .NET Aspire.

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop
- Visual Studio 2022 (optional)
- Visual Studio Code (optional)
- Rider (optional)

## Required Services

The application uses the following services that are automatically managed by .NET Aspire:

- Redis (for caching)
- RabbitMQ (for messaging)
- .NET Aspire Dashboard

## API Keys Required

You'll need to obtain the following API keys:
- CoinMarketCap API key
- Exchange Rates API key

## Project Structure

The solution consists of the following projects:

1. **KnabCryptoRatesConverterApp.AppHost**
   - Orchestrates the entire application using .NET Aspire
   - Manages container lifecycle and service dependencies

2. **KnabCryptoRatesConverterApp.ServiceDefaults**
   - Contains shared service configurations
   - Implements common features like health checks and telemetry

3. **KnabCryptoRatesConverterApp.CoinMarketCapSync**
   - Background service that fetches crypto rates from CoinMarketCap
   - Publishes rate updates to RabbitMQ
   - Implements resilient HTTP clients with retry policies

4. **KnabCryptoRatesConverterApp.ApiService**
   - REST API for accessing crypto rates
   - Consumes messages from RabbitMQ
   - Caches rates in Redis

5. **KnabCryptoRatesConverterApp.Web**
   - Web frontend for the application
   - Communicates with the API service

## Getting Started

1. Clone the repository:
```bash
git clone [repository-url]
cd KnabCryptoRatesConverterApp
```

2. Configure API keys:
   - Create `appsettings.Development.json` in the CoinMarketCapSync project
   - Add your API keys:
```json
{
  "CoinMarketCapSettings": {
    "ApiKey": "your-coinmarketcap-api-key",
    "ExchangeRatesApiKey": "your-exchangerates-api-key"
  }
}
```

3. Run the application:
```bash
cd KnabCryptoRatesConverterApp.AppHost
dotnet run
```

The .NET Aspire dashboard will automatically open, showing the status of all services.

## Architecture

### High-Level Overview

The application follows a microservices architecture pattern with the following components:

1. **Data Collection Service (CoinMarketCapSync)**
   - Periodically fetches cryptocurrency rates from CoinMarketCap API
   - Converts rates to multiple currencies using Exchange Rates API
   - Publishes updates to RabbitMQ queue
   - Implements health checks and telemetry

2. **API Service**
   - Subscribes to RabbitMQ for rate updates
   - Caches rates in Redis
   - Provides REST endpoints for rate queries
   - Implements resilient patterns and circuit breakers

3. **Web Frontend**
   - Blazor-based user interface
   - Communicates with API service
   - Implements caching for better performance

### Communication Flow

1. CoinMarketCapSync service:
   - Fetches rates every 60 seconds (configurable)
   - Processes and validates the data
   - Publishes to "crypto-rates" RabbitMQ queue

2. API Service:
   - Consumes messages from RabbitMQ
   - Updates Redis cache
   - Serves client requests with cached data
   - Implements fallback mechanisms

3. Web Frontend:
   - Makes HTTP requests to API service
   - Implements client-side caching
   - Provides real-time updates

### Infrastructure

- **Service Discovery**: Implemented via .NET Aspire
- **Caching**: Redis for distributed caching
- **Messaging**: RabbitMQ for reliable message delivery
- **Monitoring**: OpenTelemetry for metrics and tracing
- **Health Checks**: Implemented across all services
- **Resilience**: Polly-based retry policies and circuit breakers

## Running Tests



BDD Tests:
```bash
dotnet test KnabCryptoRatesConverterApp.*.BDD.Test
```

## Development

1. **Local Development**:
   - Use Visual Studio 2022 or VS Code
   - Run `dotnet watch run` in AppHost project
  

2. **Docker Development**:
   - Containers are automatically managed by Aspire
   - Redis and RabbitMQ are provisioned automatically
   - No manual Docker commands needed

## Monitoring and Observability

- **Metrics**: Available through OpenTelemetry
- **Logging**: Structured logging with correlation IDs
- **Tracing**: Distributed tracing across services
- **Health Checks**: Available at `/health` endpoints

## Configuration

Key configuration options in `appsettings.json`:

- Update intervals
- API endpoints
- Retry policies
- Cache durations
- Supported cryptocurrencies and currencies

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

Mohamed El-Sayed