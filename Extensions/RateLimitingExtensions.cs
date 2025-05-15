using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace CurrencyConverter.Extensions
{
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddFixedRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync("{\"error\": \"Too many requests. Please try again later.\"}", token);
                };

                options.AddPolicy("fixed", context =>
                    RateLimitPartition.Get(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: key => new FixedWindowRateLimiter(
                            new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromSeconds(10),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 2
                            }))
                );
            });

            return services;
        }
    }
}