using Serilog;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Filters;
using UbuhlebethuConnectPro.WebApp.Handlers;
using UbuhlebethuConnectPro.WebApp.Middleware;
using UbuhlebethuConnectPro.Core.Options;
using UbuhlebethuConnectPro.Core.Services.AI;
using Microsoft.AspNetCore.Authentication.Cookies;

// PostgreSQL Npgsql UTC timestamp compatibility
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Show money as "R 1,234.56" everywhere ("C" formats)
var randCulture = (System.Globalization.CultureInfo)new System.Globalization.CultureInfo("en-US").Clone();
randCulture.NumberFormat.CurrencySymbol = "R";
randCulture.NumberFormat.CurrencyPositivePattern = 2;   // R 1,234.56
randCulture.NumberFormat.CurrencyNegativePattern = 9;   // -R 1,234.56
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = randCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = randCulture;

// ─────────────────────────────────────────────────────────────────────────────
// Serilog bootstrap logger (before host is built)
// ─────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Optional, git-ignored file for local secrets (never commit it).
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    // ─────────────────────────────────────────────────────────────────────────
    // Serilog
    // ─────────────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/webapp-.log", rollingInterval: RollingInterval.Day));

    // ─────────────────────────────────────────────────────────────────────────
    // Standard Cookie Authentication
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options => {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

    // ─────────────────────────────────────────────────────────────────────────
    // Session (needed for JWT storage in the webapp)
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    // ─────────────────────────────────────────────────────────────────────────
    // HTTP context accessor (needed by JwtHandler)
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();

    // ─────────────────────────────────────────────────────────────────────────
    // JwtHandler (DelegatingHandler) – scoped
    // ─────────────────────────────────────────────────────────────────────────
    builder.Services.AddScoped<JwtHandler>();
    builder.Services.AddHttpClient<UbuhlebethuConnectPro.WebApp.Services.ICurrencyService, UbuhlebethuConnectPro.WebApp.Services.CurrencyService>();

    // ─────────────────────────────────────────────────────────────────────────
    // Typed HttpClient for ConnectProApiClient with JwtHandler middleware
    // ─────────────────────────────────────────────────────────────────────────
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5000";
    builder.Services.AddHttpClient<IConnectProApiClient, ConnectProApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
    })
    .AddHttpMessageHandler<JwtHandler>();

    // ─────────────────────────────────────────────────────────────────────────
    // MVC + RequireMfaSetupFilter
    // ─────────────────────────────────────────────────────────────────────────
    // builder.Services.AddScoped<RequireMfaSetupFilter>();

    builder.Services.AddControllersWithViews(options =>
    {
        // options.Filters.AddService<RequireMfaSetupFilter>();
        options.Filters.Add<DynamicRoleRefreshFilter>();
    });

    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.GeminiOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.GeminiOptions.Section));
    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.GroqOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.GroqOptions.Section));
    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.NvidiaOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.NvidiaOptions.Section));
    
    builder.Services.AddHttpClient<UbuhlebethuConnectPro.Core.Services.AI.GeminiAIService>();
    builder.Services.AddHttpClient<UbuhlebethuConnectPro.Core.Services.AI.GroqAIService>();
    builder.Services.AddHttpClient<UbuhlebethuConnectPro.Core.Services.AI.NvidiaAIService>();
    builder.Services.AddSingleton<UbuhlebethuConnectPro.Core.Services.AI.IUserAIMemoryService, UbuhlebethuConnectPro.Core.Services.AI.UserAIMemoryService>();
    builder.Services.AddScoped<UbuhlebethuConnectPro.Core.Services.AI.IAIService, UbuhlebethuConnectPro.Core.Services.AI.CompositeAIService>();

    // ─────────────────────────────────────────────────────────────────────────
    // Build the application
    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─────────────────────────────────────────────────────────────────────────
    // Middleware pipeline
    // ─────────────────────────────────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Account/Error");
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/Account/Error", "?statusCode={0}");

    app.UseMiddleware<GlobalExceptionMiddleware>();

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseSession();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // Redirect /swagger from WebApp to API Swagger UI
    app.MapGet("/swagger/{**slug}", (string? slug, IConfiguration config) =>
    {
        var apiBaseUrl = (config["Api:BaseUrl"] ?? "http://localhost:5025").TrimEnd('/');
        var target = string.IsNullOrWhiteSpace(slug) ? "index.html" : slug;
        return Results.Redirect($"{apiBaseUrl}/swagger/{target}");
    });

    app.MapGet("/swagger", (IConfiguration config) =>
    {
        var apiBaseUrl = (config["Api:BaseUrl"] ?? "http://localhost:5025").TrimEnd('/');
        return Results.Redirect($"{apiBaseUrl}/swagger/index.html");
    });

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name is not "StopTheHostException" and not "HostAbortedException")
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

namespace UbuhlebethuConnectPro.WebApp
{
    public partial class WebAppProgram { }
}
