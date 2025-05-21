using CurrencyConverter.Extensions;
using CurrencyConverter.Models;
using CurrencyConverter.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
Directory.CreateDirectory(logDirectory);

// --- Serilog Configuration ---
Log.Logger = new LoggerConfiguration()
	.Enrich.WithProperty("ServiceName", "CurrencyConverterService")
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.WriteTo.File(
		path: Path.Combine(logDirectory, "logging_.txt"),
		rollingInterval: RollingInterval.Day,
		outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
	)
	.CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
Log.Information("Running in: {Environment}", builder.Environment.EnvironmentName);

// --- JWT Settings ---
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
var jwtSettings = jwtSettingsSection.Get<JwtSettings>();
if (string.IsNullOrWhiteSpace(jwtSettings?.SecretKey))
	throw new Exception("JWT SecretKey is missing or empty in configuration.");

builder.Services.Configure<FrankfurterSettings>(
	builder.Configuration.GetSection("Frankfurter"));
builder.Services.Configure<JwtSettings>(jwtSettingsSection);
builder.Services.AddHttpContextAccessor();

// --- API Versioning ---
builder.Services.AddApiVersioning(options =>
{
	options.DefaultApiVersion = new ApiVersion(1, 0);
	options.AssumeDefaultVersionWhenUnspecified = true;
	options.ReportApiVersions = true;
});
builder.Services.AddVersionedApiExplorer(options =>
{
	options.GroupNameFormat = "'v'VVV";
	options.SubstituteApiVersionInUrl = true;
});

// --- Swagger (Versioned + JWT) ---
builder.Services.AddSwaggerWithJwt();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>(); // adds versioning support

// --- OpenTelemetry ---
var otelServiceName = "CurrencyConverterService";

builder.Services.AddOpenTelemetry()
	.WithTracing(tracerProviderBuilder =>
	{
		tracerProviderBuilder
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(otelServiceName))
			.SetSampler(new AlwaysOnSampler())
			.AddAspNetCoreInstrumentation()
			.AddHttpClientInstrumentation()
			.AddConsoleExporter();
	})
	.WithMetrics(metricProviderBuilder =>
	{
		metricProviderBuilder
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(otelServiceName))
			.AddAspNetCoreInstrumentation()
			.AddHttpClientInstrumentation()
			.AddRuntimeInstrumentation()
			.AddConsoleExporter();
	});

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();
builder.Logging.AddOpenTelemetry(options =>
{
	options.IncludeFormattedMessage = true;
	options.IncludeScopes = true;
	options.ParseStateValues = true;
	options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(otelServiceName));
});

// --- App Services ---
builder.Services
	.AddMemoryCache()
	.AddAuthorization()
	.AddFrankfurterClient(builder.Configuration)
	.AddJwtAuthentication(builder.Configuration)
	.AddFixedRateLimiting()
	.AddApplicationServices()
	.AddControllers();

var app = builder.Build();

// --- Swagger UI ---
var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	foreach (var description in provider.ApiVersionDescriptions)
	{
		options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
			$"Currency Converter API {description.GroupName.ToUpperInvariant()}");
	}
});

app.Use(async (context, next) =>
{
	if (context.Request.Path.StartsWithSegments("/swagger") &&
		!context.User.Identity?.IsAuthenticated == true)
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		await context.Response.WriteAsync("Unauthorized");
		return;
	}

	await next();
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.Run();
