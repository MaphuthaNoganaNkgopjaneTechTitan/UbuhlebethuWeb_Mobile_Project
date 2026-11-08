# UbuhleBethu ConnectPro — Web & Backend Services (.NET 10)

This directory contains the complete .NET 10 solution for **UbuhleBethu ConnectPro**, encompassing the Web MVC Application, the REST API, Core domain contracts, Infrastructure services, and the Automated Integration Test suite.

---

## Projects Overview

1. **[`UbuhleBethu-WebApp/`](./UbuhleBethu-WebApp)**
   - ASP.NET Core 10 MVC Web Application.
   - Comprehensive Razor view dashboards for Admins, Managers, Quantity Surveyors, Site Managers, and Clients.
   - Embedded shrinkable Multi-Model AI Assistant Dock (Gemini 3.8 Flash, Groq Qwen 3.8, Nvidia Nemotron).
   - Dynamic Role Refresh filters and client-side PDF/CSV reporting engines.
   - *Refer to [UbuhleBethu-WebApp/README.md](./UbuhleBethu-WebApp/README.md) for detailed workflows and operational guides.*

2. **[`UbuhlebethuConnectPro.Api/`](./UbuhlebethuConnectPro.Api)**
   - ASP.NET Core 10 REST API.
   - 21 API controllers serving authentication, job statistics, project deletions, user administration, quotes, invoices, and AI inference.
   - Swashbuckle OpenAPI 2.x documentation at `/swagger`.
   - Dual port bindings: HTTPS `7031` and HTTP `5025`.

3. **[`UbuhlebethuConnectPro.Core/`](./UbuhlebethuConnectPro.Core)**
   - Domain models: `Job`, `Project`, `Quotation`, `Invoice`, `Expense`, `SiteReport`, `AuditLog`, `ApplicationUser`.
   - Repository interfaces and AI service contracts.

4. **[`UbuhlebethuConnectPro.Infrastructure/`](./UbuhlebethuInfrastructure)**
   - Database context (`ApplicationDbContext`) connecting to PostgreSQL on Neon.
   - Cloud S3 object storage provider (Backblaze B2) for GPS-tagged photos and site documents.
   - Brevo SMTP automated email notifications.
   - Live multi-currency exchange rate conversion feeds.

5. **[`UbuhlebethuConnectPro.Tests/`](./UbuhlebethuConnectPro.Tests)**
   - Automated integration and unit test suite (xUnit, FluentAssertions, Moq, WebApplicationFactory).
   - Validates authorization safeguards, anti-IDOR checks, Swagger schema generation, and currency conversions.

---

## Quick Start Commands

```powershell
# 1. Restore entire solution
dotnet restore "UbuhleBethuConnectPro.Web.slnx"

# 2. Run API backend
dotnet run --project "UbuhlebethuConnectPro.Api" --launch-profile https

# 3. Run Web dashboard (in another terminal)
dotnet run --project "UbuhleBethu-WebApp"

# 4. Run automated test suite
dotnet test "UbuhlebethuConnectPro.Tests" -c Release
```

---

*UbuhleBethu ConnectPro &copy; 2026.*
