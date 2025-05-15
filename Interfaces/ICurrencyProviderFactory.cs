namespace CurrencyConverter.Interfaces
{
    public interface ICurrencyProviderFactory
    {
        ICurrencyProvider GetProvider(string providerName);
    }
}