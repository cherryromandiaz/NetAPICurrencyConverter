using CurrencyConverter.Interfaces;
using CurrencyConverter.Models;
using CurrencyConverter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CurrencyConverter.Tests.Services
{
	public class FrankfurterServiceTests
	{
		private readonly Mock<IMemoryCache> _memoryCacheMock;
		private readonly Mock<ILogger<FrankfurterService>> _loggerMock;
		private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
		private readonly Mock<IConfiguration> _configurationMock;
		private readonly string _frankfurterBaseUrl = "https://api.frankfurter.app/";
		private HttpClient _httpClient;

		public FrankfurterServiceTests()
		{
			// Initialize mocks
			_memoryCacheMock = new Mock<IMemoryCache>();
			_loggerMock = new Mock<ILogger<FrankfurterService>>();
			_httpContextAccessorMock = new Mock<IHttpContextAccessor>();
			_configurationMock = new Mock<IConfiguration>();

			// Set up configuration to return base URL
			_configurationMock.Setup(c => c["CurrencyProviders:Frankfurter"])
				.Returns(_frankfurterBaseUrl);

			// Set up HttpContext with a trace identifier
			var httpContext = new DefaultHttpContext();
			httpContext.TraceIdentifier = "test-correlation-id";
			_httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
		}

		private FrankfurterService CreateService(HttpClient httpClient = null)
		{
			_httpClient = httpClient ?? CreateMockHttpClient();
			return new FrankfurterService(
				_httpClient,
				_memoryCacheMock.Object,
				_loggerMock.Object,
				_configurationMock.Object,
				_httpContextAccessorMock.Object
			);
		}

		private HttpClient CreateMockHttpClient(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "")
		{
			var handlerMock = new Mock<HttpMessageHandler>();

			handlerMock
				.Protected()
				.Setup<Task<HttpResponseMessage>>(
					"SendAsync",
					ItExpr.IsAny<HttpRequestMessage>(),
					ItExpr.IsAny<CancellationToken>()
				)
				.ReturnsAsync(new HttpResponseMessage
				{
					StatusCode = statusCode,
					Content = new StringContent(content)
				});

			var client = new HttpClient(handlerMock.Object)
			{
				BaseAddress = new Uri(_frankfurterBaseUrl)
			};

			return client;
		}

		private void SetupMemoryCacheMock<T>(string key, T value, bool exists = true)
		{
			var memoryCacheEntryMock = new Mock<ICacheEntry>();

			if (exists)
			{
				object outValue = value;
				_memoryCacheMock
					.Setup(m => m.TryGetValue(key, out outValue))
					.Returns(true);
			}
			else
			{
				object outValue = null;
				_memoryCacheMock
					.Setup(m => m.TryGetValue(key, out outValue))
					.Returns(false);
			}

			_memoryCacheMock
				.Setup(m => m.CreateEntry(It.IsAny<object>()))
				.Returns(memoryCacheEntryMock.Object);
		}

		[Fact]
		public async Task GetLatestRatesAsync_ReturnsFromCache_WhenCached()
		{
			// Arrange
			var cachedResponse = new ExchangeRateResponse
			{
				Base = "USD",
				Date = DateTime.UtcNow,
				Rates = new Dictionary<string, decimal> { { "EUR", 0.85m } }
			};

			SetupMemoryCacheMock("latest-USD", cachedResponse);
			var service = CreateService();

			// Act
			var result = await service.GetLatestRatesAsync("USD");

			// Assert
			Assert.Equal(cachedResponse, result);
			Assert.Equal("USD", result.Base);
			Assert.Contains("EUR", result.Rates.Keys);
			Assert.Equal(0.85m, result.Rates["EUR"]);
		}

		[Fact]
		public async Task GetLatestRatesAsync_FetchesFromApi_WhenNotCached()
		{
			// Arrange
			var expectedResponse = new ExchangeRateResponse
			{
				Base = "USD",
				Date = DateTime.UtcNow,
				Rates = new Dictionary<string, decimal> { { "EUR", 0.85m } }
			};

			var jsonResponse = JsonSerializer.Serialize(expectedResponse);
			var httpClient = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);

			// Set cache to not find the key
			object nullValue = null;
			_memoryCacheMock
				.Setup(m => m.TryGetValue("latest-USD", out nullValue))
				.Returns(false);

			var cacheEntryMock = new Mock<ICacheEntry>();
			_memoryCacheMock
				.Setup(m => m.CreateEntry(It.IsAny<object>()))
				.Returns(cacheEntryMock.Object);

			var service = CreateService(httpClient);

			// Act
			var result = await service.GetLatestRatesAsync("USD");

			// Assert
			Assert.NotNull(result);
			Assert.Equal("USD", result.Base);
			Assert.Contains("EUR", result.Rates.Keys);

			// Verify the cache was set
			_memoryCacheMock.Verify(
				m => m.CreateEntry(It.IsAny<object>()),
				Times.Once);
		}

		[Fact]
		public async Task ConvertCurrencyAsync_ThrowsException_WhenUsingExcludedCurrency()
		{
			// Arrange
			var service = CreateService();

			// Act & Assert
			var exception = await Assert.ThrowsAsync<ArgumentException>(
				() => service.ConvertCurrencyAsync("TRY", "USD", 100)
			);

			Assert.Contains("Currency not allowed", exception.Message);
		}

		[Fact]
		public async Task ConvertCurrencyAsync_ReturnsFromCache_WhenCached()
		{
			// Arrange
			decimal cachedAmount = 85.5m;
			SetupMemoryCacheMock("convert-USD-EUR-100", cachedAmount);
			var service = CreateService();

			// Act
			var result = await service.ConvertCurrencyAsync("USD", "EUR", 100);

			// Assert
			Assert.Equal(cachedAmount, result);
		}

		[Fact]
		public async Task ConvertCurrencyAsync_FetchesFromApi_WhenNotCached()
		{
			// Arrange
			var expectedResponse = new ExchangeRateResponse
			{
				Base = "USD",
				Date = DateTime.UtcNow,
				Rates = new Dictionary<string, decimal> { { "EUR", 85.5m } }
			};

			var jsonResponse = JsonSerializer.Serialize(expectedResponse);
			var httpClient = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);

			// Set cache to not find the key
			object nullValue = null;
			_memoryCacheMock
				.Setup(m => m.TryGetValue("convert-USD-EUR-100", out nullValue))
				.Returns(false);

			var cacheEntryMock = new Mock<ICacheEntry>();
			_memoryCacheMock
				.Setup(m => m.CreateEntry(It.IsAny<object>()))
				.Returns(cacheEntryMock.Object);

			var service = CreateService(httpClient);

			// Act
			var result = await service.ConvertCurrencyAsync("USD", "EUR", 100);

			// Assert
			Assert.Equal(85.5m, result);

			// Verify the cache was set
			_memoryCacheMock.Verify(
				m => m.CreateEntry(It.IsAny<object>()),
				Times.Once);
		}

		[Fact]
		public async Task ConvertCurrencyAsync_ThrowsException_WhenRateNotFound()
		{
			// Arrange
			var expectedResponse = new ExchangeRateResponse
			{
				Base = "USD",
				Date = DateTime.UtcNow,
				Rates = new Dictionary<string, decimal> { { "GBP", 0.75m } } // EUR is missing
			};

			var jsonResponse = JsonSerializer.Serialize(expectedResponse);
			var httpClient = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);

			// Set cache to not find the key
			object nullValue = null;
			_memoryCacheMock
				.Setup(m => m.TryGetValue("convert-USD-EUR-100", out nullValue))
				.Returns(false);

			var service = CreateService(httpClient);

			// Act & Assert
			await Assert.ThrowsAsync<KeyNotFoundException>(
				() => service.ConvertCurrencyAsync("USD", "EUR", 100)
			);
		}

		[Fact]
		public async Task GetHistoricalRatesAsync_ReturnsFromCache_WhenCached()
		{
			// Arrange
			var startDate = new DateTime(2023, 1, 1);
			var endDate = new DateTime(2023, 1, 7);
			var cachedResponse = new List<ExchangeRateResponse>
			{
				new ExchangeRateResponse
				{
					Base = "USD",
					Date = new DateTime(2023, 1, 1),
					Rates = new Dictionary<string, decimal> { { "EUR", 0.85m } }
				}
			};

			SetupMemoryCacheMock($"history-USD-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}-1-10", cachedResponse);
			var service = CreateService();

			// Act
			var result = await service.GetHistoricalRatesAsync("USD", startDate, endDate, 1, 10);

			// Assert
			Assert.Equal(cachedResponse, result);
			Assert.Single(result);
			Assert.Equal("USD", result[0].Base);
		}

		[Fact]
		public async Task GetHistoricalRatesAsync_FetchesFromApi_WhenNotCached()
		{
			// Arrange
			var startDate = new DateTime(2023, 1, 1);
			var endDate = new DateTime(2023, 1, 7);

			var expectedResponse = new HistoricalRateResponse
			{
				Base = "USD",
				Rates = new Dictionary<string, Dictionary<string, decimal>>
				{
					{ "2023-01-01", new Dictionary<string, decimal> { { "EUR", 0.85m } } },
					{ "2023-01-02", new Dictionary<string, decimal> { { "EUR", 0.86m } } }
				}
			};

			var jsonResponse = JsonSerializer.Serialize(expectedResponse);
			var httpClient = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);

			// Set cache to not find the key
			string cacheKey = $"history-USD-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}-1-10";
			object nullValue = null;
			_memoryCacheMock
				.Setup(m => m.TryGetValue(cacheKey, out nullValue))
				.Returns(false);

			var cacheEntryMock = new Mock<ICacheEntry>();
			_memoryCacheMock
				.Setup(m => m.CreateEntry(It.IsAny<object>()))
				.Returns(cacheEntryMock.Object);

			var service = CreateService(httpClient);

			// Act
			var result = await service.GetHistoricalRatesAsync("USD", startDate, endDate, 1, 10);

			// Assert
			Assert.Equal(2, result.Count);
			Assert.Equal("USD", result[0].Base);
			Assert.Contains("EUR", result[0].Rates.Keys);

			// Verify the cache was set
			_memoryCacheMock.Verify(
				m => m.CreateEntry(It.IsAny<object>()),
				Times.Once);
		}

		[Fact]
		public async Task GetHistoricalRatesAsync_ReturnsEmptyList_WhenApiReturnsNull()
		{
			// Arrange
			var startDate = new DateTime(2023, 1, 1);
			var endDate = new DateTime(2023, 1, 7);

			// Return null rates in response
			var expectedResponse = new HistoricalRateResponse
			{
				Base = "USD",
				Rates = null
			};

			var jsonResponse = JsonSerializer.Serialize(expectedResponse);
			var httpClient = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);

			// Set cache to not find the key
			string cacheKey = $"history-USD-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}-1-10";
			object nullValue = null;
			_memoryCacheMock
				.Setup(m => m.TryGetValue(cacheKey, out nullValue))
				.Returns(false);

			var service = CreateService(httpClient);

			// Act
			var result = await service.GetHistoricalRatesAsync("USD", startDate, endDate, 1, 10);

			// Assert
			Assert.Empty(result);
		}
	}
}