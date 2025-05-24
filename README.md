# .Net Currency Converter API

A robust, scalable, and maintainable currency conversion API built with C# and ASP.NET Core, featuring high performance, security, and resilience capabilities.

## 🚀 Features

- **Real-time Currency Conversion**: Convert amounts between different currencies using the latest exchange rates
- **Historical Exchange Rates**: Retrieve historical exchange rates with pagination support
- **High Performance**: Intelligent caching and retry mechanisms with circuit breaker pattern
- **Security First**: JWT authentication with role-based access control (RBAC)
- **Rate Limiting**: API throttling to prevent abuse
- **Comprehensive Logging**: Structured logging with distributed tracing
- **Resilient Architecture**: Retry policies with exponential backoff and circuit breaker
- **Extensible Design**: Factory pattern for multiple exchange rate providers

## 📋 API Endpoints

### 1. Latest Exchange Rates
```
GET https://localhost:7092/api/v1/exchange-rates/latest?baseCurrency=USD&provider=frankfurter
```
Fetch the latest exchange rates for a specific base currency.

### 2. Currency Conversion
```
GET https://localhost:7092/api/v1/exchange-rates/convert?from=USD&to=EUR&amount=100&provider=frankfurter
{
  "amount": 100,
  "from": "USD",
  "to": "EUR",
  "provider": "frankfurter"
}
```
Convert amounts between different currencies.

**Note**: TRY, PLN, THB, and MXN are excluded and will return a bad request if used.

### 3. Historical Exchange Rates
```
GET https://localhost:7092/api/v1/exchange-rates/history?baseCurrency=USD&start=2025-01-01&end=2025-01-30&page=1&pageSize=10&provider=frankfurter
```
Retrieve historical exchange rates for a given period with pagination.

## 🛠️ Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)
- [Redis](https://redis.io/) (for caching)
- [SQL Server](https://www.microsoft.com/en-us/sql-server) or [PostgreSQL](https://www.postgresql.org/) (for data storage)

## ⚡ Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/cherryromandiaz/NetAPICurrencyConverter.git
cd NetAPICurrencyConverter
```

### 2. Configure Application Settings
Copy the example configuration and update with your settings:
```bash
cp appsettings.Development.json appsettings.Test.json appsettings.Production.json appsettings.json
```

### 3. Restore Dependencies
```bash
dotnet restore
```

### 4. Start the Application
```bash
dotnet run
```

The API will be available at `https://localhost:7092/` (HTTPS)

## 🔧 Configuration

### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Set to `Development`, `Test`, or `Production`
- `SecretKey`: JWT signing key

## 🔐 Authentication

### Obtaining JWT Token
```bash
POST /api/auth/login
{
  "username": "admin",
  "password": "password"
}
```

### Using JWT Token
Include the token in the Authorization header:
```
Authorization: Bearer your-jwt-token
```

## 📊 Monitoring and Logging

### Structured Logging
The application uses Serilog for structured logging with the following sinks:
- Console (Development)
- File (All environments)

### Metrics
Prometheus metrics are available at `/metrics` endpoint.

## 🧪 Testing

### Run Integration Tests and Unit Tests
Running from Test Menu in Visual Studio


### Generate Test Coverage Report
```bash
.\Generate-CoverageReport.ps1

powershell -ExecutionPolicy Bypass -File .\Generate-CoverageReport.ps1
```

Open `./coverage-report/index.html` to view the coverage report.

## 🏗️ Architecture

### Project Structure
```
NetAPICurrencyConverter/
├── CurrencyConverter.IntegrationTests/  # Test projects
└── CurrencyConverter.Tests/             # Integration Test projects
```

### Key Design Patterns
- **Repository Pattern**: Data access abstraction
- **Factory Pattern**: Dynamic currency provider selection
- **Circuit Breaker Pattern**: Resilience against external API failures
- **Retry Pattern**: Handling transient failures
- **Dependency Injection**: Loose coupling and testability


## 📈 Performance Considerations

### Caching Strategy
- **In-Memory Cache**: For frequently accessed exchange rates
- **Distributed Cache (Redis)**: For scalability across multiple instances
- **HTTP Cache Headers**: For client-side caching

### Resilience Patterns
- **Circuit Breaker**: Prevents cascading failures
- **Retry with Exponential Backoff**: Handles transient failures
- **Timeout Policies**: Prevents hanging requests

## 🛡️ Security Features

- **JWT Authentication**: Stateless authentication
- **Role-Based Access Control (RBAC)**: Fine-grained permissions
- **Rate Limiting**: Prevents API abuse
- **Input Validation**: Prevents injection attacks
- **HTTPS Enforcement**: Encrypted communication
- **CORS Configuration**: Cross-origin request security

## 📝 Assumptions Made

1. **External API Dependency**: The application relies on the Frankfurter API as the primary data source
2. **Currency Exclusions**: TRY, PLN, THB, and MXN are permanently excluded as per requirements
3. **Authentication**: Users must be authenticated to access conversion endpoints
4. **Rate Limiting**: Applied globally across all authenticated users
5. **Caching TTL**: Exchange rates are cached for 10 minutes for latest rates, 24 hours for historical rates
6. **Database**: Assuming SQL Server for production, SQLite for development
7. **Pagination**: Default page size is 20 items, maximum is 100

## 🚧 Future Enhancements

### Phase 1 - Enhanced Features
- [ ] **Multi-Currency Provider Support**: Integrate additional providers (Fixer.io, CurrencyAPI)
- [ ] **Webhook Notifications**: Real-time rate change notifications
- [ ] **Advanced Analytics**: Currency trend analysis and forecasting
- [ ] **Bulk Conversion**: Process multiple conversions in a single request

### Phase 2 - Advanced Capabilities
- [ ] **GraphQL API**: Alternative query interface
- [ ] **Real-time Updates**: WebSocket support for live rate updates
- [ ] **Machine Learning**: Predictive rate forecasting
- [ ] **Mobile SDK**: Native mobile app integration

### Phase 3 - Enterprise Features
- [ ] **Multi-tenancy**: Support for multiple client organizations
- [ ] **Advanced Security**: OAuth 2.0, API key management
- [ ] **Compliance**: GDPR, PCI-DSS compliance features
- [ ] **Enterprise Monitoring**: Advanced APM integration

### Technical Improvements
- [ ] **gRPC Support**: High-performance internal communication
- [ ] **Event-Driven Architecture**: Domain events and message queues
- [ ] **Container Orchestration**: Kubernetes deployment manifests
- [ ] **API Gateway Integration**: Kong, Ambassador support
- [ ] **Database Sharding**: Horizontal database scaling
- [ ] **Advanced Caching**: Multi-level caching with cache warming

## 📚 API Documentation

Once the application is running, you can access:
- **Swagger UI**: `https://localhost:7092/swagger/index.html`


## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

For support and questions:
- Create an issue in the GitHub repository
- Check the [Wiki](../../wiki) for detailed documentation
- Review existing [Discussions](../../discussions) for common questions

## 🙏 Acknowledgments

- [Frankfurter API](https://www.frankfurter.app/) for providing free currency exchange rates
- [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) for the excellent web framework
- [Serilog](https://serilog.net/) for structured logging capabilities