using CurrencyConverter.Interfaces;
using CurrencyConverter.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace CurrencyConverter.Services
{
    public class FrankfurterService : ICurrencyProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FrankfurterService> _logger;
        private static readonly string[] ExcludedCurrencies = { "TRY", "PLN", "THB", "MXN" };
        private readonly IHttpContextAccessor _httpContextAccessor;

		public FrankfurterService(HttpClient httpClient, 
			IMemoryCache cache, 
			ILogger<FrankfurterService> logger,
			IOptions<FrankfurterSettings> frankfurterOptions,
			IConfiguration configuration, 
			IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            var settings = frankfurterOptions.Value;
			// Get the base address from configuration
			//string baseAddress = configuration["CurrencyProviders:Frankfurter"]
			//                              ?? throw new InvalidOperationException("Frankfurter API base address not found in configuration");

			//         _httpClient.BaseAddress = new Uri(baseAddress);

			//         _logger.LogInformation("FrankfurterService initialized with base URL: {BaseUrl}", _httpClient.BaseAddress);

			if (string.IsNullOrWhiteSpace(settings.BaseUrl))
				throw new InvalidOperationException("Frankfurter BaseUrl is not configured.");

			_httpClient.BaseAddress = new Uri(settings.BaseUrl); // Use value from settings
			_logger.LogInformation("FrankfurterService initialized with base URL: {BaseUrl}", _httpClient.BaseAddress);
		}

        public async Task<ExchangeRateResponse> GetLatestRatesAsync(string baseCurrency)
        {
	        AttachCorrelationId();
			try
            {
                string cacheKey = $"latest-{baseCurrency.ToUpper()}";
                if (_cache.TryGetValue(cacheKey, out ExchangeRateResponse cached))
                {
                    _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
                    return cached;
                }

                _logger.LogInformation("Fetching latest rates for {BaseCurrency} from {Url}",
                    baseCurrency, $"{_httpClient.BaseAddress}latest?base={baseCurrency}");

                var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>($"latest?base={baseCurrency}");

                if (response != null)
                {
                    _cache.Set(cacheKey, response, TimeSpan.FromMinutes(10));
                    _logger.LogInformation("Cached exchange rates for {BaseCurrency} with {RateCount} currencies",
                        baseCurrency, response.Rates?.Count ?? 0);
                    return response;
                }

                _logger.LogWarning("No data returned from API for {BaseCurrency}", baseCurrency);
                return new ExchangeRateResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest rates for {BaseCurrency}", baseCurrency);
                throw;
            }
        }

        public async Task<decimal> ConvertCurrencyAsync(string from, string to, decimal amount)
        {
	        AttachCorrelationId();
			try
            {
                if (ExcludedCurrencies.Contains(from) || ExcludedCurrencies.Contains(to))
                {
                    _logger.LogWarning("Attempted conversion with excluded currency: {From} or {To}", from, to);
                    throw new ArgumentException($"Currency not allowed: {from} or {to} is in the excluded list.");
                }

                string cacheKey = $"convert-{from}-{to}-{amount}";
                if (_cache.TryGetValue(cacheKey, out decimal cachedAmount))
                {
                    _logger.LogInformation("Cache hit for conversion {From} to {To} amount {Amount}", from, to, amount);
                    return cachedAmount;
                }

                _logger.LogInformation("Converting {Amount} {From} to {To}", amount, from, to);
                var result = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>($"latest?amount={amount}&from={from}&to={to}");

                if (result?.Rates == null || !result.Rates.ContainsKey(to))
                {
                    _logger.LogWarning("Missing rate for {To} in conversion response", to);
                    throw new KeyNotFoundException($"Exchange rate for {to} not found in response");
                }

                var converted = result.Rates[to];
                _cache.Set(cacheKey, converted, TimeSpan.FromMinutes(5));

                _logger.LogInformation("Converted {Amount} {From} to {ConvertedAmount} {To}",
                    amount, from, converted, to);

                return converted;
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
            {
                _logger.LogError(ex, "Error converting {Amount} {From} to {To}", amount, from, to);
                throw;
            }
        }

        public async Task<List<ExchangeRateResponse>> GetHistoricalRatesAsync(string baseCurrency, DateTime startDate, DateTime endDate, int page, int pageSize)
        {
	        AttachCorrelationId();
			try
            {
                string cacheKey = $"history-{baseCurrency}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}-{page}-{pageSize}";
                if (_cache.TryGetValue(cacheKey, out List<ExchangeRateResponse> cachedHistory))
                {
                    _logger.LogInformation("Cache hit for historical rates {CacheKey}", cacheKey);
                    return cachedHistory;
                }

                _logger.LogInformation("Fetching historical rates for {BaseCurrency} from {StartDate} to {EndDate}",
                    baseCurrency, startDate, endDate);

                var url = $"{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?base={baseCurrency}";
               
                var response = await _httpClient.GetFromJsonAsync<HistoricalRateResponse>(url);

                if (response?.Rates == null)
                {
                    _logger.LogWarning("No historical rates returned for {BaseCurrency}", baseCurrency);
                    return new List<ExchangeRateResponse>();
                }

                var results = response.Rates
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(rate => new ExchangeRateResponse
                    {
                        Base = response.Base,
                        Date = DateTime.Parse(rate.Key),
                        Rates = rate.Value
                    })
                    .ToList();

                _cache.Set(cacheKey, results, TimeSpan.FromHours(1));

                _logger.LogInformation("Retrieved {Count} historical rates for {BaseCurrency}",
                    results.Count, baseCurrency);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical rates for {BaseCurrency} from {StartDate} to {EndDate}",
                    baseCurrency, startDate, endDate);
                throw;
            }
        }

        private void AttachCorrelationId()
        {
	        var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier
	                            ?? Guid.NewGuid().ToString();

	        if (_httpClient.DefaultRequestHeaders.Contains("X-Correlation-ID"))
	        {
		        _httpClient.DefaultRequestHeaders.Remove("X-Correlation-ID");
	        }

	        _httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
	        _logger.LogInformation("Attached Correlation ID: {CorrelationId} to outbound request", correlationId);
        }
	}
}