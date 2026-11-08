using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using UbuhlebethuConnectPro.Core.Models;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UbuhlebethuConnectPro.WebApp.Data;
using UbuhlebethuConnectPro.Core.Options;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.WebApp.Security;
using UbuhlebethuConnectPro.Core.Services;
using UbuhlebethuConnectPro.Core.Services.AI;
using UbuhlebethuConnectPro.Infrastructure.Services;
using UbuhlebethuConnectPro.Api.Middleware;

// PostgreSQL Npgsql UTC timestamp compatibility
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Bootstrap Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ubuhlebethu-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting UbuhlebethuConnectPro API...");
    var builder = WebApplication.CreateBuilder(args);

    // Optional, git-ignored file for local secrets (never commit it).
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    // If nothing tells Kestrel which port to use (e.g. started without a launch profile it would pick 5000),
    // use the port the web app expects.
    if (string.IsNullOrWhiteSpace(builder.Configuration["urls"])
        && string.IsNullOrWhiteSpace(builder.Configuration["HTTP_PORTS"])
        && string.IsNullOrWhiteSpace(builder.Configuration["HTTPS_PORTS"]))
    {
        builder.WebHost.UseUrls("http://0.0.0.0:5025");
    }

    // Fail fast with a message that says exactly what to do (an empty value used to surface as the cryptic
    // "The ConnectionString property has not been initialized").
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection"))
        || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
    {
        throw new InvalidOperationException(
            "Missing configuration: 'ConnectionStrings:DefaultConnection' and/or 'Jwt:Key' is empty. " +
            "Create UbuhlebethuConnectPro.Api/appsettings.Local.json (it is git-ignored, so it is NOT in git) " +
            "or set the environment variables ConnectionStrings__DefaultConnection and Jwt__Key.");
    }

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/api-.txt", rollingInterval: RollingInterval.Day));

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "UbuhleBethu ConnectPro API",
            Version = "v1",
            Description = "Backend REST API for UbuhleBethu ConnectPro"
        });

        // Resolve schemaId conflicts between nested classes across controllers (e.g. QuoteDto in Office vs Quotes)
        c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

        // JWT Bearer auth support in Swagger UI
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer eyJhbGciOi...\"",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc),
                new List<string>()
            }
        });
    });
    builder.Services.AddProblemDetails();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                "http://localhost:5000",
                "https://localhost:5001",
                "http://localhost:5025",
                "https://localhost:7031",
                "http://localhost:5138",
                "https://localhost:7139",
                "https://ubuhlebethu-connectpro.onrender.com",
                "https://ubuhlebethuconnectpro.onrender.com"
            )
            .SetIsOriginAllowed(origin =>
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host == "localhost" || uri.Host.EndsWith("onrender.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("runasp.net", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });
    });

    // Rate Limiting (AspNetCoreRateLimiting)
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Rate limiting for Auth endpoints (login / register): 5 requests / min partitioned by client IP to block brute force
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous_auth",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        // Rate limiting for Gemini AI endpoints: 15 requests / min per user/IP to prevent token exhaustion
        options.AddPolicy("ai", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User?.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous_ai",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 15,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            var retryAfterSec = 60;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                retryAfterSec = (int)Math.Max(1, retryAfter.TotalSeconds);
            }
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSec.ToString();

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests. Rate limit exceeded to ensure service stability.",
                retryAfterSeconds = retryAfterSec,
                status = 429
            }, cancellationToken: token);
        };
    });

    // Health Checks (Database, Storage, Memory)
    builder.Services.AddHealthChecks()
        .AddCheck<UbuhlebethuConnectPro.Api.Health.DatabaseHealthCheck>("database", tags: new[] { "ready", "db" })
        .AddCheck<UbuhlebethuConnectPro.Api.Health.StorageHealthCheck>("storage", tags: new[] { "ready", "storage" })
        .AddCheck<UbuhlebethuConnectPro.Api.Health.MemoryHealthCheck>("memory", tags: new[] { "ready", "system" });

    // Configure EF Core
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."),
            b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddSignInManager<SignInManager<ApplicationUser>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // Repositories & Services
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<IQuotationRepository, QuotationRepository>();
    builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
    builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
    builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
    builder.Services.AddScoped<ISiteReportRepository, SiteReportRepository>();
    builder.Services.AddScoped<ISiteReportService, SiteReportService>();
    builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();

    // Email Notification Service
    builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();

    // Storage Service (Backblaze B2 / S3 & Local Storage Fallback)
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
    var storageConfig = builder.Configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
    builder.Services.AddSingleton<LocalStorageService>();

    if (string.Equals(storageConfig.Provider, "s3", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(storageConfig.ServiceUrl) &&
        !string.IsNullOrWhiteSpace(storageConfig.Bucket))
    {
        builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
        {
            var config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = storageConfig.ServiceUrl,
                ForcePathStyle = true
            };
            return new Amazon.S3.AmazonS3Client(storageConfig.AccessKeyId, storageConfig.SecretAccessKey, config);
        });
        builder.Services.AddScoped<IStorageService, S3StorageService>();
    }
    else
    {
        builder.Services.AddScoped<IStorageService>(sp => sp.GetRequiredService<LocalStorageService>());
    }

    // Phase 3 DI
    builder.Services.AddScoped<IInvoiceFactory, StandardInvoiceFactory>();
    builder.Services.AddScoped<INotificationObserver, DbNotificationObserver>();

    builder.Services.AddScoped<IQuotationService>(provider =>
    {
        var service = new QuotationService(
            provider.GetRequiredService<IQuotationRepository>(),
            provider.GetRequiredService<IInvoiceRepository>(),
            provider.GetRequiredService<IInvoiceFactory>(),
            provider.GetRequiredService<IProjectRepository>()
        );
        service.AttachObserver(provider.GetRequiredService<INotificationObserver>());
        return service;
    });

    builder.Services.AddScoped<IProjectService, ProjectService>();

    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.GeminiOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.GeminiOptions.Section));
    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.GroqOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.GroqOptions.Section));
    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.NvidiaOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.NvidiaOptions.Section));

    builder.Services.AddHttpClient<UbuhlebethuConnectPro.Core.Services.AI.GeminiAIService>();
    builder.Services.AddHttpClient<UbuhlebethuConnectPro.Core.Services.AI.GroqAIService>();
    builder.Services.AddHttpClient<UbuhlebethuConnectPro.Core.Services.AI.NvidiaAIService>();
    builder.Services.Configure<UbuhlebethuConnectPro.Core.Options.AIAssistantOptions>(builder.Configuration.GetSection(UbuhlebethuConnectPro.Core.Options.AIAssistantOptions.Section));
    // Durable per-user assistant memory (survives restarts; keyed by authenticated username)
    builder.Services.AddSingleton<UbuhlebethuConnectPro.Core.Services.AI.IUserAIMemoryStore, UbuhlebethuConnectPro.Api.Data.DbUserAIMemoryStore>();
    builder.Services.AddSingleton<UbuhlebethuConnectPro.Core.Services.AI.IUserAIMemoryService, UbuhlebethuConnectPro.Core.Services.AI.UserAIMemoryService>();
    builder.Services.AddScoped<UbuhlebethuConnectPro.Core.Services.AI.IAIService, UbuhlebethuConnectPro.Core.Services.AI.CompositeAIService>();
    // Phase 3B: Tool executor for AI function calling (accesses project/expense/site-report repos)
    builder.Services.AddScoped<UbuhlebethuConnectPro.Core.Services.AI.AIToolExecutor>();

    // JWT Auth
    var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found in configuration.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        });

    // Enforce HTTPS
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
    }

    var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(webRootPath);
    Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "sitereports"));
    builder.Environment.WebRootPath = webRootPath;

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseExceptionHandler();

    // Always enable Swagger UI & JSON endpoints
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UbuhleBethu ConnectPro API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "UbuhleBethu ConnectPro API - Swagger UI";
        c.DisplayRequestDuration();
    });

    // Root redirect to Swagger UI
    app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    app.UseCors();
    app.UseRateLimiter();
    app.UseStaticFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRootPath),
        RequestPath = ""
    });

    // Automatically apply EF Migrations, then seed. Separate try blocks so a seeding problem is never hidden by (or hides) a migration problem.
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext>();
            dbContext.Database.Migrate();
            Log.Information("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations.");
        }

        try
        {
            await UbuhlebethuConnectPro.Api.Data.ProjectActivityStore.EnsureTableAsync(
                scope.ServiceProvider.GetRequiredService<UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext>());
            await UbuhlebethuConnectPro.Api.Data.DbUserAIMemoryStore.EnsureTableAsync(
                scope.ServiceProvider.GetRequiredService<UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext>());
            await UbuhlebethuConnectPro.Api.Data.DbSeeder.SeedRolesAndUsersAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding roles and users.");
        }
    }

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

    // Comprehensive Health Checks Endpoint (/healthz)
    app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;

            using var currentProc = System.Diagnostics.Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - currentProc.StartTime.ToUniversalTime();

            var payload = new
            {
                status = report.Status.ToString(),
                totalDuration = $"{report.TotalDuration.TotalMilliseconds:F1}ms",
                checkedAt = DateTime.UtcNow,
                system = new
                {
                    server = "UbuhleBethu-Api",
                    environment = builder.Environment.EnvironmentName,
                    framework = Environment.Version.ToString(),
                    os = Environment.OSVersion.ToString(),
                    uptime = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s"
                },
                entries = report.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        status = kvp.Value.Status.ToString(),
                        description = kvp.Value.Description,
                        duration = $"{kvp.Value.Duration.TotalMilliseconds:F1}ms",
                        data = kvp.Value.Data
                    })
            };

            await context.Response.WriteAsJsonAsync(payload);
        }
    }).AllowAnonymous();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<UbuhlebethuConnectPro.Api.Hubs.SiteEventsHub>("/hubs/site-events").AllowAnonymous();

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

namespace UbuhlebethuConnectPro.Api
{
    public partial class ApiProgram { }
}


