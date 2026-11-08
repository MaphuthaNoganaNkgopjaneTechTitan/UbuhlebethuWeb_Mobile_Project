using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UbuhlebethuConnectPro.WebApp.Services;

namespace UbuhlebethuConnectPro.Tests;

public class CurrencyServiceTests
{
    /// <summary>Simulates the rate feed being unreachable so the stored fallback rates are used.</summary>
    private sealed class FeedDownHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("feed unreachable");
    }

    private sealed class LiveFeedHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"ZAR\":20}}")
            });
        }
    }

    private static CurrencyService Create() =>
        new(
            new HttpClient(new FeedDownHandler()),
            NullLogger<CurrencyService>.Instance,
            new ConfigurationBuilder().Build());

    [Fact]
    public async Task SameCurrency_ReturnsSameAmount()
    {
        var r = await Create().ConvertAsync(250m, "ZAR", "ZAR");
        Assert.Equal(250m, r.Converted);
        Assert.Equal(1m, r.Rate);
    }

    [Fact]
    public async Task FeedDown_UsesFallbackRates_AndSaysSo()
    {
        var r = await Create().ConvertAsync(1800m, "ZAR", "USD");
        Assert.False(r.IsLive);
        Assert.Equal(100m, r.Converted);
    }

    [Fact]
    public async Task CurrencyCodes_AreCaseInsensitive()
    {
        var r = await Create().ConvertAsync(10m, "zar", "zar");
        Assert.Equal("ZAR", r.From);
    }

    [Fact]
    public void Currencies_ContainsExpandedCatalog()
    {
        var currencies = Create().Currencies;
        var supportedCodes = new[]
        {
            "EUR", "USD", "JPY", "BGN", "CZK", "DKK", "GBP", "HUF", "PLN", "RON", "SEK",
            "CHF", "ISK", "NOK", "HRK", "RUB", "TRY", "AUD", "BRL", "CAD", "CNY", "HKD",
            "IDR", "ILS", "INR", "KRW", "MXN", "MYR", "NZD", "PHP", "SGD", "THB"
        };

        Assert.All(supportedCodes, code => Assert.Contains(code, currencies.Keys));
    }

    [Fact]
    public async Task LiveFeed_UsesConfiguredApiKeyForExpandedCurrency()
    {
        var handler = new LiveFeedHandler();
        var service = new CurrencyService(
            new HttpClient(handler),
            NullLogger<CurrencyService>.Instance,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Currency:ApiKey"] = "test-key"
                })
                .Build());

        var result = await service.ConvertAsync(2m, "EUR", "ZAR");

        Assert.True(result.IsLive);
        Assert.Equal(40m, result.Converted);
        Assert.Contains("apikey=test-key", handler.RequestUri?.Query);
        Assert.Contains("base_currency=EUR", handler.RequestUri?.Query);
    }

    [Fact]
    public async Task UnsupportedCurrency_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Create().ConvertAsync(10m, "ZAR", "XXX"));
    }

    [Fact]
    public async Task NegativeAmount_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Create().ConvertAsync(-1m, "ZAR", "USD"));
    }
}
