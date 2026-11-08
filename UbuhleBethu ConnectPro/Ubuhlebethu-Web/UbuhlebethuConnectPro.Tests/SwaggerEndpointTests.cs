using System;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UbuhlebethuConnectPro.Api;
using Xunit;

namespace UbuhlebethuConnectPro.Tests
{
    [Collection("IntegrationTests")]
    public class SwaggerEndpointTests : IClassFixture<WebApplicationFactory<ApiProgram>>
    {
        private readonly WebApplicationFactory<ApiProgram> _apiFactory;

        static SwaggerEndpointTests()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=testdb;Username=test;Password=test");
            Environment.SetEnvironmentVariable("Jwt__Key", "uGOiXoITACM6JxFkWwy8ySzwYLdPuFRmpBtVpQeynpixD6rZ/+8M0qqipg2AD6qY");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "UbuhlebethuConnectPro");
            Environment.SetEnvironmentVariable("Jwt__Audience", "UbuhlebethuConnectPro.Clients");
        }

        public SwaggerEndpointTests(WebApplicationFactory<ApiProgram> apiFactory)
        {
            _apiFactory = apiFactory;
        }

        [Fact]
        public async Task SwaggerJson_ReturnsOk_AndContainsValidOpenApiSchema()
        {
            // Arrange
            var client = _apiFactory.CreateClient();

            // Act
            var response = await client.GetAsync("/swagger/v1/swagger.json");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"openapi\":");
            content.Should().Contain("\"title\": \"UbuhleBethu ConnectPro API\"");
            content.Should().Contain("/api/v1/dashboard/jobs/delete");
            content.Should().Contain("/api/v1/users/{id}/delete");
        }

        [Fact]
        public async Task SwaggerUi_IndexHtml_ReturnsOk()
        {
            // Arrange
            var client = _apiFactory.CreateClient();

            // Act
            var response = await client.GetAsync("/swagger/index.html");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("swagger-ui");
        }

        [Fact]
        public async Task ApiRoot_RedirectsToSwaggerUi()
        {
            // Arrange
            var client = _apiFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync("/");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.ToString().Should().Be("/swagger/index.html");
        }
    }
}
