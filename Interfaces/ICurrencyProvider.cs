using CurrencyConverter.Models;

namespace CurrencyConverter.Interfaces
{
    public interface ICurrencyProvider
    {
        Task<ExchangeRateResponse> GetLatestRatesAsync(string baseCurrency);
        Task<decimal> ConvertCurrencyAsync(string from, string to, decimal amount);
        Task<List<ExchangeRateResponse>> GetHistoricalRatesAsync(string baseCurrency, DateTime startDate, DateTime endDate, int page, int pageSize);
    }
}