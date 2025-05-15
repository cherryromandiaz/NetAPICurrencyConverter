using CurrencyConverter.Factories;
using CurrencyConverter.Interfaces;
using CurrencyConverter.Models;
using CurrencyConverter.Providers;
using CurrencyConverter.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;
using System.Text;

namespace CurrencyConverter.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        {
            var jwtSettings = config.GetSection("JwtSettings").Get<JwtSettings>();
            services.AddSingleton(jwtSettings);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateLifetime = true
                };
            });

            return services;
        }

        public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' followed by your JWT token."
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });

            return services;
        }

        public static IServiceCollection AddFrankfurterClient(this IServiceCollection services, IConfiguration configuration)
        {
            var frankfurterUrl = configuration["CurrencyProviders:Frankfurter"];

            if (string.IsNullOrEmpty(frankfurterUrl))
            {
                throw new ArgumentException("Frankfurter API base URL is not configured.");
            }

            // Register HttpClient for FrankfurterCurrencyProvider
            services.AddHttpClient<FrankfurterCurrencyProvider>(client =>
                {
                    client.BaseAddress = new Uri(frankfurterUrl);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            // Register HttpClient for FrankfurterService (this is what's missing!)
            services.AddHttpClient<FrankfurterService>(client =>
                {
                    client.BaseAddress = new Uri(frankfurterUrl);
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<FrankfurterService>(); // Register dependency
            services.AddScoped<FrankfurterCurrencyProvider>(); // Register provider
            services.AddScoped<ICurrencyProviderFactory, CurrencyProviderFactory>(); // Register factory
            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        Console.WriteLine($"[Retry] Attempt {retryAttempt} after {timespan.TotalSeconds}s: {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, timespan) =>
                    {
                        Console.WriteLine($"[CircuitBreaker] Breaking circuit for {timespan.TotalSeconds}s due to: {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                    },
                    onReset: () =>
                    {
                        Console.WriteLine("[CircuitBreaker] Circuit closed. Calls will flow again.");
                    },
                    onHalfOpen: () =>
                    {
                        Console.WriteLine("[CircuitBreaker] Circuit in half-open state. Next call is a trial.");
                    });
        }

    }
}
