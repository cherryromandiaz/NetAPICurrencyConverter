using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace CurrencyConverter.Middlewares
{
	public class CorrelationIdMiddleware
	{
		private readonly RequestDelegate _next;
		private const string CorrelationIdHeader = "X-Correlation-ID";

		public CorrelationIdMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			// Check if client sent a correlation ID, else generate a new one
			var correlationId = context.Request.Headers[CorrelationIdHeader].ToString();
			if (string.IsNullOrEmpty(correlationId))
			{
				correlationId = Guid.NewGuid().ToString();
				context.Request.Headers[CorrelationIdHeader] = correlationId;
			}

			// Store it for later access in the request lifecycle
			context.Items[CorrelationIdHeader] = correlationId;

			// Add it to response headers so client can see it
			context.Response.OnStarting(() =>
			{
				context.Response.Headers[CorrelationIdHeader] = correlationId;
				return Task.CompletedTask;
			});

			await _next(context);
		}
	}
}

