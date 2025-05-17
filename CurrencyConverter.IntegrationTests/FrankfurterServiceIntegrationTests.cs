using CurrencyConverter.Models;
using CurrencyConverter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace CurrencyConverter.IntegrationTests
{
	public class FrankfurterServiceIntegrationTests : IDisposable
	{
		private readonly WireMockServer _mockServer;
		private readonly FrankfurterService _service;

		public FrankfurterServiceIntegrationTests()
		{
			_mockServer = WireMockServer.Start();

			var config = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string>
				{
				{ "CurrencyProviders:Frankfurter", _mockServer.Url! }
				})
				.Build();

			var httpClient = new HttpClient
			{
				BaseAddress = new Uri(_mockServer.Url!)
			};

			var cache = new MemoryCache(new MemoryCacheOptions());
			var logger = new LoggerFactory().CreateLogger<FrankfurterService>();

			var context = new DefaultHttpContext();
			var httpContextAccessor = new HttpContextAccessor
			{
				HttpContext = context
			};

			_service = new FrankfurterService(httpClient, cache, logger, config, httpContextAccessor);
		}

		public void Dispose()
		{
			_mockServer.Stop();
			_mockServer.Dispose();
		}

		[Fact]
		public async Task GetLatestRatesAsync_ReturnsMockedRates()
		{
			_mockServer
				.Given(Request.Create().WithPath("/latest").UsingGet().WithParam("base", "USD"))
				.RespondWith(Response.Create()
					.WithStatusCode(HttpStatusCode.OK)
					.WithHeader("Content-Type", "application/json")
					.WithBody("""
                    {
                        "amount": 1.0,
                        "base": "USD",
                        "date": "2024-01-01",
                        "rates": { "EUR": 0.85, "GBP": 0.75 }
                    }
                """));

			var result = await _service.GetLatestRatesAsync("USD");

			Assert.NotNull(result);
			Assert.Equal("USD", result.Base);
			Assert.True(result.Rates.ContainsKey("EUR"));
		}

		[Fact]
		public async Task ConvertCurrencyAsync_ReturnsMockedConversion()
		{
			_mockServer
				.Given(Request.Create().WithPath("/latest").UsingGet()
					.WithParam("amount", "100")
					.WithParam("from", "USD")
					.WithParam("to", "EUR"))
				.RespondWith(Response.Create()
					.WithStatusCode(HttpStatusCode.OK)
					.WithHeader("Content-Type", "application/json")
					.WithBody("""
                    {
                        "amount": 100.0,
                        "base": "USD",
                        "date": "2024-01-01",
                        "rates": { "EUR": 85.0 }
                    }
                """));

			var result = await _service.ConvertCurrencyAsync("USD", "EUR", 100);

			Assert.Equal(85.0m, result);
		}

		[Fact]
		public async Task GetHistoricalRatesAsync_ReturnsMockedHistoricalData()
		{
			_mockServer
				.Given(Request.Create()
					.WithPath("/2024-01-01..2024-01-03")
					.WithParam("base", "USD")
					.UsingGet())
				.RespondWith(Response.Create()
					.WithStatusCode(HttpStatusCode.OK)
					.WithHeader("Content-Type", "application/json")
					.WithBody("""
                    {
                        "base": "USD",
                        "rates": {
                            "2024-01-01": { "EUR": 0.85 },
                            "2024-01-02": { "EUR": 0.86 },
                            "2024-01-03": { "EUR": 0.87 }
                        }
                    }
                """));

			var result = await _service.GetHistoricalRatesAsync("USD", new DateTime(2024, 1, 1), new DateTime(2024, 1, 3), 1, 2);

			Assert.Equal(2, result.Count);
			Assert.Equal("USD", result[0].Base);
			Assert.Contains("EUR", result[0].Rates.Keys);
		}
	}

}