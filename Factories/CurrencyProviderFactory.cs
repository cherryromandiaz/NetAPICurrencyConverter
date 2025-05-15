using CurrencyConverter.Interfaces;
using CurrencyConverter.Providers;

namespace CurrencyConverter.Factories
{
    public class CurrencyProviderFactory : ICurrencyProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CurrencyProviderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ICurrencyProvider GetProvider(string providerName)
        {
            return providerName.ToLower() switch
            {
                "frankfurter" => _serviceProvider.GetRequiredService<FrankfurterCurrencyProvider>(),
                // "ecb" => _serviceProvider.GetRequiredService<EcbCurrencyProvider>(),
                _ => throw new NotSupportedException($"Currency provider '{providerName}' is not supported.")
            };
        }
    }
}