using CurrencyConverter.Extensions;
using CurrencyConverter.Models;
using CurrencyConverter.Middlewares;
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

// --- JWT Settings ---
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
var jwtSettings = jwtSettingsSection.Get<JwtSettings>();
if (string.IsNullOrWhiteSpace(jwtSettings?.SecretKey))
	throw new Exception("JWT SecretKey is missing or empty in configuration.");

builder.Services.Configure<JwtSettings>(jwtSettingsSection);
builder.Services.AddHttpContextAccessor();

// --- OpenTelemetry Tracing, Metrics, and Logging ---
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
	.AddSwaggerWithJwt()
	.AddFixedRateLimiting()
	.AddApplicationServices()
	.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.Run();
