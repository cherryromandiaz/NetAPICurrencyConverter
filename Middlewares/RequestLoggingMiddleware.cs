using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyConverter.Middlewares
{
	public class RequestLoggingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<RequestLoggingMiddleware> _logger;
		private readonly string _logDirectory;

		public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
			_logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
			Directory.CreateDirectory(_logDirectory); // Ensure Logs/ exists
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var stopwatch = Stopwatch.StartNew();
			await _next(context);
			stopwatch.Stop();

			var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
			var method = context.Request.Method;
			var endpoint = context.Request.Path;
			var statusCode = context.Response.StatusCode;
			var responseTime = stopwatch.ElapsedMilliseconds;

			string clientId = "Unknown";
			var authHeader = context.Request.Headers["Authorization"].ToString();

			if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
			{
				var token = authHeader.Substring("Bearer ".Length);
				var handler = new JwtSecurityTokenHandler();
				try
				{
					var jwtToken = handler.ReadJwtToken(token);
					// Get ClientId from 'sub' claim, fallback to 'Unknown' if missing
					clientId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? "Unknown";
				}
				catch
				{
					clientId = "Invalid JWT";
				}
			}


			var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] " +
			                 $"IP: {clientIp}, ClientId: {clientId}, Method: {method}, " +
			                 $"Endpoint: {endpoint}, Status: {statusCode}, Time: {responseTime}ms";

			// Log to ILogger (console, debug, etc.)
			_logger.LogInformation(logMessage);
		}
	}

}