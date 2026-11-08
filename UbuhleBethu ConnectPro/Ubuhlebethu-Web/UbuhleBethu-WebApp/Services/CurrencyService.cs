using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace UbuhlebethuConnectPro.WebApp.Services
{
    public record CurrencyResult(string From, string To, decimal Amount, decimal Rate, decimal Converted, DateTime AsOfUtc, bool IsLive);
    public record CurrencyRates(string Base, IReadOnlyDictionary<string, decimal> Rates, DateTime AsOfUtc, bool IsLive);

    public interface ICurrencyService
    {
        IReadOnlyDictionary<string, string> Currencies { get; }
        Task<CurrencyResult> ConvertAsync(decimal amount, string from, string to, CancellationToken ct = default);
        Task<CurrencyRates> GetRatesAsync(string baseCode, CancellationToken ct = default);
    }

    /// <summary>
    /// Converts between any world currency using Freecurrencyapi when configured.
    /// Rates are cached for 6 hours. If the feed is unreachable, built-in approximate rates
    /// (ZAR per 1 unit) are used for the main currencies and the result is flagged as not live.
    /// </summary>
    public class CurrencyService : ICurrencyService
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
        private readonly HttpClient _http;
        private readonly ILogger<CurrencyService> _log;
        private readonly string _apiKey;
        private static readonly ConcurrentDictionary<string, (DateTime At, Dictionary<string, decimal> Rates)> Cache = new();

        // Shown first in every dropdown.
        private static readonly Dictionary<string, string> PopularNames = new()
        {
            ["ZAR"] = "South African Rand",
            ["EUR"] = "Euro",
            ["USD"] = "US Dollar",
            ["JPY"] = "Japanese Yen",
            ["BGN"] = "Bulgarian Lev",
            ["CZK"] = "Czech Republic Koruna",
            ["DKK"] = "Danish Krone",
            ["GBP"] = "British Pound",
            ["HUF"] = "Hungarian Forint",
            ["PLN"] = "Polish Zloty",
            ["RON"] = "Romanian Leu",
            ["SEK"] = "Swedish Krona",
            ["CHF"] = "Swiss Franc",
            ["ISK"] = "Icelandic Króna",
            ["NOK"] = "Norwegian Krone",
            ["HRK"] = "Croatian Kuna",
            ["RUB"] = "Russian Ruble",
            ["TRY"] = "Turkish Lira",
            ["AUD"] = "Australian Dollar",
            ["BRL"] = "Brazilian Real",
            ["CAD"] = "Canadian Dollar",
            ["CNY"] = "Chinese Yuan",
            ["HKD"] = "Hong Kong Dollar",
            ["IDR"] = "Indonesian Rupiah",
            ["ILS"] = "Israeli New Sheqel",
            ["INR"] = "Indian Rupee",
            ["KRW"] = "South Korean Won",
            ["MXN"] = "Mexican Peso",
            ["MYR"] = "Malaysian Ringgit",
            ["NZD"] = "New Zealand Dollar",
            ["PHP"] = "Philippine Peso",
            ["SGD"] = "Singapore Dollar",
            ["THB"] = "Thai Baht",
            ["BWP"] = "Botswana Pula",
            ["NAD"] = "Namibian Dollar",
            ["MZN"] = "Mozambican Metical",
            ["ZMW"] = "Zambian Kwacha",
            ["AED"] = "UAE Dirham"
        };

        public IReadOnlyDictionary<string, string> Currencies { get; } = BuildCurrencies();

        // Approximate fallback: how many ZAR one unit of the currency is worth.
        private static readonly Dictionary<string, decimal> FallbackZar = new()
        {
            ["ZAR"] = 1m,
            ["USD"] = 18m,
            ["EUR"] = 20m,
            ["GBP"] = 23.5m,
            ["BWP"] = 1.33m,
            ["NAD"] = 1m,
            ["MZN"] = 0.28m,
            ["ZMW"] = 0.75m,
            ["AUD"] = 11.8m,
            ["CAD"] = 13m,
            ["CNY"] = 2.5m,
            ["INR"] = 0.21m,
            ["JPY"] = 0.12m,
            ["AED"] = 4.9m
        };

        public CurrencyService(HttpClient http, ILogger<CurrencyService> log, IConfiguration configuration)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(5);
            _log = log;
            _apiKey = configuration["Currency:ApiKey"] ?? "";
        }

        private static Dictionary<string, string> BuildCurrencies()
        {
            // Every currency the operating system knows about (one entry per ISO code).
            var all = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    var code = (region.ISOCurrencySymbol ?? "").ToUpperInvariant();
                    if (code.Length == 3 && code.All(char.IsLetter) && code != "XXX")
                        all[code] = region.CurrencyEnglishName;
                }
                catch (ArgumentException) { }
            }

            var result = new Dictionary<string, string>();
            foreach (var kv in PopularNames) result[kv.Key] = kv.Value;
            foreach (var kv in all) if (!result.ContainsKey(kv.Key)) result[kv.Key] = kv.Value;
            return result;
        }

        public async Task<CurrencyResult> ConvertAsync(decimal amount, string from, string to, CancellationToken ct = default)
        {
            from = (from ?? "").Trim().ToUpperInvariant();
            to = (to ?? "").Trim().ToUpperInvariant();
            if (!Currencies.ContainsKey(from) || !Currencies.ContainsKey(to))
                throw new ArgumentException("Unsupported currency code.");
            if (amount < 0 || amount > 1_000_000_000_000m)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount is out of range.");

            var (rates, at, live) = await LoadRatesAsync(from, ct);
            if (!rates.TryGetValue(to, out var rate)) throw new ArgumentException("Rate not available for " + to);
            return new CurrencyResult(from, to, amount, rate, Math.Round(amount * rate, 2), at, live);
        }

        public async Task<CurrencyRates> GetRatesAsync(string baseCode, CancellationToken ct = default)
        {
            baseCode = (baseCode ?? "").Trim().ToUpperInvariant();
            if (!Currencies.ContainsKey(baseCode))
                throw new ArgumentException("Unsupported currency code.");

            var (rates, at, live) = await LoadRatesAsync(baseCode, ct);
            return new CurrencyRates(baseCode, rates, at, live);
        }

        private async Task<(Dictionary<string, decimal> Rates, DateTime At, bool Live)> LoadRatesAsync(string baseCode, CancellationToken ct)
        {
            if (Cache.TryGetValue(baseCode, out var hit) && DateTime.UtcNow - hit.At < CacheFor)
                return (hit.Rates, hit.At, true);
            try
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                    throw new InvalidOperationException("Currency API key is not configured.");

                var endpoint = $"https://api.freecurrencyapi.com/v1/latest?apikey={Uri.EscapeDataString(_apiKey)}&base_currency={Uri.EscapeDataString(baseCode)}";
                using var resp = await _http.GetAsync(endpoint, ct);
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var rates = new Dictionary<string, decimal>();
                foreach (var p in doc.RootElement.GetProperty("data").EnumerateObject())
                {
                    var code = p.Name.ToUpperInvariant();
                    if (Currencies.ContainsKey(code)) rates[code] = p.Value.GetDecimal();
                }
                Cache[baseCode] = (DateTime.UtcNow, rates);
                return (rates, DateTime.UtcNow, true);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Live exchange rates unavailable; using built-in approximate rates.");
                if (!FallbackZar.ContainsKey(baseCode))
                    throw new ArgumentException($"Live exchange rates for {baseCode} are unavailable right now. Please try again later.");
                var rates = FallbackZar.ToDictionary(k => k.Key, k => FallbackZar[baseCode] / k.Value);
                return (rates, DateTime.UtcNow, false);
            }
        }
    }
}