using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UbuhlebethuConnectPro.Api;
using Xunit;

namespace UbuhlebethuConnectPro.Tests
{
    [Collection("IntegrationTests")]
    public class SecurityAndHealthCheckTests : IClassFixture<WebApplicationFactory<ApiProgram>>
    {
        private readonly WebApplicationFactory<ApiProgram> _factory;

        static SecurityAndHealthCheckTests()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=ep-dark-bird-b1kvlfcb-pooler.c-5.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_rXyEz5DkcLx8;SslMode=Require;");
            Environment.SetEnvironmentVariable("Jwt__Key", "uGOiXoITACM6JxFkWwy8ySzwYLdPuFRmpBtVpQeynpixD6rZ/+8M0qqipg2AD6qY");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "UbuhlebethuConnectPro");
            Environment.SetEnvironmentVariable("Jwt__Audience", "UbuhlebethuConnectPro.Clients");
        }

        public SecurityAndHealthCheckTests(WebApplicationFactory<ApiProgram> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Healthz_Endpoint_Returns_Status_And_Component_Breakdown()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/healthz");

            // Assert
            response.StatusCode.Should().Match(s => s == HttpStatusCode.OK || s == HttpStatusCode.ServiceUnavailable);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotBeNullOrWhiteSpace();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.TryGetProperty("status", out var statusProp).Should().BeTrue();
            root.TryGetProperty("entries", out var entriesProp).Should().BeTrue();

            // Verify components exist
            entriesProp.TryGetProperty("database", out _).Should().BeTrue("Database check should be present in /healthz");
            entriesProp.TryGetProperty("storage", out _).Should().BeTrue("Storage check should be present in /healthz");
            entriesProp.TryGetProperty("memory", out _).Should().BeTrue("Memory check should be present in /healthz");

            // Verify system metadata
            root.TryGetProperty("system", out var systemProp).Should().BeTrue();
            systemProp.TryGetProperty("server", out var serverProp).Should().BeTrue();
            serverProp.GetString().Should().Be("UbuhleBethu-Api");
        }

        [Fact]
        public async Task Health_Simple_Endpoint_Returns_Healthy()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("healthy");
        }

        [Fact]
        public async Task RateLimiter_AuthPolicy_Returns_429_When_Limit_Exceeded()
        {
            // Arrange
            var client = _factory.CreateClient();
            var payload = new { Username = "bruteforce_test_user", Password = "WrongPassword123!" };

            HttpResponseMessage? lastResponse = null;

            // Act - Send 7 consecutive requests (Limit is 5 per window)
            for (int i = 0; i < 7; i++)
            {
                lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", payload);
                if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    break;
                }
            }

            // Assert
            lastResponse.Should().NotBeNull();
            lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            lastResponse.Headers.Contains("Retry-After").Should().BeTrue();

            var content = await lastResponse.Content.ReadAsStringAsync();
            content.Should().Contain("Rate limit exceeded");
        }
    }
}
