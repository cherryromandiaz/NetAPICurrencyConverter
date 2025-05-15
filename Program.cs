using CurrencyConverter.Extensions;
using CurrencyConverter.Models;

var builder = WebApplication.CreateBuilder(args);

// Validate JwtSettings
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
var jwtSettings = jwtSettingsSection.Get<JwtSettings>();

if (string.IsNullOrWhiteSpace(jwtSettings?.SecretKey))
{
    throw new Exception("JWT SecretKey is missing or empty in configuration.");
}

// Register strongly typed JwtSettings using IOptions
builder.Services.Configure<JwtSettings>(jwtSettingsSection);

builder.Services
    .AddMemoryCache()
    .AddAuthorization()
    .AddFrankfurterClient(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddSwaggerWithJwt()
    .AddFixedRateLimiting()
    .AddApplicationServices()
    .AddControllers(); // Optional: move to end for clarity

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.Run();