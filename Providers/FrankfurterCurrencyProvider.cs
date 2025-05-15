using CurrencyConverter.Interfaces;
using CurrencyConverter.Models;
using CurrencyConverter.Services;

namespace CurrencyConverter.Providers
{
    public class FrankfurterCurrencyProvider : ICurrencyProvider
    {
        private readonly FrankfurterService _frankfurterService;

        public FrankfurterCurrencyProvider(FrankfurterService frankfurterService)
        {
            _frankfurterService = frankfurterService;
        }

        public Task<ExchangeRateResponse> GetLatestRatesAsync(string baseCurrency) =>
            _frankfurterService.GetLatestRatesAsync(baseCurrency);

        public Task<decimal> ConvertCurrencyAsync(string from, string to, decimal amount) =>
            _frankfurterService.ConvertCurrencyAsync(from, to, amount);

        public Task<List<ExchangeRateResponse>> GetHistoricalRatesAsync(string baseCurrency, DateTime startDate, DateTime endDate, int page, int pageSize) =>
            _frankfurterService.GetHistoricalRatesAsync(baseCurrency, startDate, endDate, page, pageSize);
    }
}