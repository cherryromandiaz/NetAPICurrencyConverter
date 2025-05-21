using CurrencyConverter.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CurrencyConverter.Controllers
{
	[Authorize]
	[EnableRateLimiting("fixed")]
	[ApiController]

	// Versioned route (v1 as example)
	[ApiVersion("1.0")]
	[Route("api/v{version:apiVersion}/exchange-rates")]
	public class ExchangeRatesController : ControllerBase
	{
		private readonly ICurrencyProviderFactory _providerFactory;

		public ExchangeRatesController(ICurrencyProviderFactory providerFactory)
		{
			_providerFactory = providerFactory;
		}

		[HttpGet("latest")]
		public async Task<IActionResult> GetLatestRates(
			[FromQuery] string baseCurrency = "EUR",
			[FromQuery] string provider = "frankfurter")
		{
			var currencyProvider = _providerFactory.GetProvider(provider);
			var result = await currencyProvider.GetLatestRatesAsync(baseCurrency);
			return Ok(result);
		}

		[HttpGet("convert")]
		public async Task<IActionResult> ConvertCurrency(
			[FromQuery] string from,
			[FromQuery] string to,
			[FromQuery] decimal amount,
			[FromQuery] string provider = "frankfurter")
		{
			try
			{
				var currencyProvider = _providerFactory.GetProvider(provider);
				var convertedAmount = await currencyProvider.ConvertCurrencyAsync(from, to, amount);
				return Ok(new { From = from, To = to, Amount = amount, Converted = convertedAmount });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { error = ex.Message });
			}
		}

		[HttpGet("history")]
		public async Task<IActionResult> GetHistoricalRates(
			[FromQuery] string baseCurrency,
			[FromQuery] DateTime start,
			[FromQuery] DateTime end,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 10,
			[FromQuery] string provider = "frankfurter")
		{
			var currencyProvider = _providerFactory.GetProvider(provider);
			var result = await currencyProvider.GetHistoricalRatesAsync(baseCurrency, start, end, page, pageSize);
			return Ok(result);
		}
	}
}
