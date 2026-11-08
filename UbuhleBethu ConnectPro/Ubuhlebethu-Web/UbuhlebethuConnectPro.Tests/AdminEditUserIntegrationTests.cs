using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using Xunit;

namespace UbuhlebethuConnectPro.Tests;

public class TestAdminAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAdminAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, 
        ILoggerFactory logger, UrlEncoder encoder) 
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "user-admin"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAdmin");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestAdmin");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

[Collection("IntegrationTests")]
public class AdminEditUserIntegrationTests : IClassFixture<WebApplicationFactory<UbuhlebethuConnectPro.WebApp.WebAppProgram>>
{
    private readonly WebApplicationFactory<UbuhlebethuConnectPro.WebApp.WebAppProgram> _factory;
    private readonly Mock<IConnectProApiClient> _mockApi;

    public AdminEditUserIntegrationTests(WebApplicationFactory<UbuhlebethuConnectPro.WebApp.WebAppProgram> factory)
    {
        _mockApi = new Mock<IConnectProApiClient>();

        _mockApi.Setup(x => x.GetUserAsync("e9c04422-4594-4488-8a79-f331ef4b56f4"))
               .ReturnsAsync(new UserDto
               {
                   Id = "e9c04422-4594-4488-8a79-f331ef4b56f4",
                   UserName = "manager",
                   Email = "manager@connectpro.local",
                   Roles = new List<string> { "Manager", "Management" }
               });

        _mockApi.Setup(x => x.GetUserRolesAsync("e9c04422-4594-4488-8a79-f331ef4b56f4"))
               .ReturnsAsync(new UserRolesDto
               {
                   UserId = "e9c04422-4594-4488-8a79-f331ef4b56f4",
                   UserName = "manager",
                   CurrentRoles = new List<string> { "Manager", "Management" },
                   AvailableRoles = new List<string> { "Admin", "Client", "FieldWorker", "Manager", "Office", "SiteManager" }
               });

        _mockApi.Setup(x => x.GetUsersAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
               .ReturnsAsync(new List<UserDto>
               {
                   new()
                   {
                       Id = "user-admin",
                       UserName = "admin",
                       Email = "admin@connectpro.local",
                       Roles = new List<string> { "Admin" }
                   }
               });

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => _mockApi.Object);

                services.AddAuthentication("TestAdmin")
                    .AddScheme<AuthenticationSchemeOptions, TestAdminAuthHandler>("TestAdmin", options => { });

                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes("TestAdmin")
                        .RequireAuthenticatedUser()
                        .Build();
                });
            });
        });
    }

    [Fact]
    public async Task GetEditUser_RendersSingleAntiforgeryTokenAndStaleRoleNotice()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Admin/EditUser/e9c04422-4594-4488-8a79-f331ef4b56f4");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        
        content.Should().Contain("Edit User &amp; Roles: manager");
        content.Should().Contain("The following previous roles are no longer available and will not be submitted: Management");
        
        // Count form tokens in the editUserForm
        var formMatch = System.Text.RegularExpressions.Regex.Match(content, @"<form[^>]+id=""editUserForm""[^>]*>([\s\S]*?)<\/form>");
        formMatch.Success.Should().BeTrue();
        var tokenCount = System.Text.RegularExpressions.Regex.Matches(formMatch.Groups[1].Value, @"name=""__RequestVerificationToken""").Count;
        tokenCount.Should().Be(1, "The editUserForm must have exactly ONE anti-forgery token to prevent HTTP 400 duplicate token rejections");
    }

    [Fact]
    public async Task PostEditUser_WithNoRoles_ShowsValidationErrorAndPreservesCheckboxes()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Fetch form to get token
        var getRes = await client.GetAsync("/Admin/EditUser/e9c04422-4594-4488-8a79-f331ef4b56f4");
        var html = await getRes.Content.ReadAsStringAsync();

        var match = System.Text.RegularExpressions.Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        match.Success.Should().BeTrue();
        var token = match.Groups[1].Value;

        // 2. Submit with no roles selected
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Id"] = "e9c04422-4594-4488-8a79-f331ef4b56f4",
            ["UserName"] = "manager",
            ["Email"] = "manager@connectpro.local"
        };

        var postRes = await client.PostAsync("/Admin/EditUser/e9c04422-4594-4488-8a79-f331ef4b56f4", new FormUrlEncodedContent(form));
        
        postRes.StatusCode.Should().Be(HttpStatusCode.OK, "Form should re-render with validation errors, NOT return HTTP 400");
        var body = await postRes.Content.ReadAsStringAsync();
        
        body.Should().Contain("At least one role must be assigned to the user.");
        // Must still have available roles rendered, not 'no available roles'
        body.Should().Contain("Manager");
        body.Should().Contain("SiteManager");
        body.Should().NotContain("There are currently no available roles to assign to this user.");
    }

    [Fact]
    public async Task PostEditUser_WithValidRole_UpdatesApiAndRedirects()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var getRes = await client.GetAsync("/Admin/EditUser/e9c04422-4594-4488-8a79-f331ef4b56f4");
        var html = await getRes.Content.ReadAsStringAsync();

        var match = System.Text.RegularExpressions.Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        var token = match.Groups[1].Value;

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Id"] = "e9c04422-4594-4488-8a79-f331ef4b56f4",
            ["UserName"] = "manager",
            ["Email"] = "manager@connectpro.local",
            ["Roles[0].RoleName"] = "Manager",
            ["Roles[0].Selected"] = "true"
        };

        var postRes = await client.PostAsync("/Admin/EditUser/e9c04422-4594-4488-8a79-f331ef4b56f4", new FormUrlEncodedContent(form));
        
        postRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
        postRes.Headers.Location?.ToString().Should().Be("/Admin");

        _mockApi.Verify(x => x.UpdateUserAsync("e9c04422-4594-4488-8a79-f331ef4b56f4", It.IsAny<UpdateUserRequest>()), Times.Once);
        _mockApi.Verify(x => x.UpdateUserRolesAsync("e9c04422-4594-4488-8a79-f331ef4b56f4", It.Is<UpdateUserRolesRequest>(r => r.Roles.Contains("Manager"))), Times.Once);
    }
}
