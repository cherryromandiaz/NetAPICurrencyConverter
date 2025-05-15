using CurrencyConverter.Extensions;
using CurrencyConverter.Models;
using CurrencyConverter.Middlewares;
using Serilog;

var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.WriteTo.File(
		path: Path.Combine(logDirectory, "logging_.txt"),
		rollingInterval: RollingInterval.Day,
		outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
	)
	.CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
var jwtSettings = jwtSettingsSection.Get<JwtSettings>();

if (string.IsNullOrWhiteSpace(jwtSettings?.SecretKey))
{
	throw new Exception("JWT SecretKey is missing or empty in configuration.");
}

builder.Services.Configure<JwtSettings>(jwtSettingsSection);
builder.Services.AddHttpContextAccessor();
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