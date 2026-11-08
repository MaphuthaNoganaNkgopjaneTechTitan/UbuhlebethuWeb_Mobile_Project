using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class LocationResponseDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Source { get; set; } = "OpenWeather Geocoding API";
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/location")]
    public class LocationApiController : ControllerBase
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _config;

        public LocationApiController(IHttpClientFactory clientFactory, IConfiguration config)
        {
            _clientFactory = clientFactory;
            _config = config;
        }

        [HttpGet("fallback")]
        public async Task<IActionResult> GetFallbackLocation([FromQuery] string? city)
        {
            var apiKey = _config["OpenWeather:ApiKey"] ?? "baca5aa239e51b5c1b146511a57b6c58";
            var targetCity = string.IsNullOrWhiteSpace(city) ? (_config["OpenWeather:DefaultCity"] ?? "Johannesburg,ZA") : city;

            try
            {
                var client = _clientFactory.CreateClient();
                var url = $"https://api.openweathermap.org/geo/1.0/direct?q={Uri.EscapeDataString(targetCity)}&limit=1&appid={apiKey}";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var firstMatch = root[0];
                        var lat = firstMatch.GetProperty("lat").GetDouble();
                        var lon = firstMatch.GetProperty("lon").GetDouble();
                        var name = firstMatch.GetProperty("name").GetString() ?? targetCity;
                        var country = firstMatch.TryGetProperty("country", out var c) ? c.GetString() ?? "ZA" : "ZA";

                        return Ok(new LocationResponseDto
                        {
                            Latitude = lat,
                            Longitude = lon,
                            LocationName = name,
                            Country = country,
                            Source = "OpenWeather Geocoding API"
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Fallthrough to default coordinates
            }

            return Ok(new LocationResponseDto
            {
                Latitude = -26.2041,
                Longitude = 28.0473,
                LocationName = "Johannesburg (Default)",
                Country = "ZA",
                Source = "OpenWeather Fallback Coordinates"
            });
        }
    }
}
