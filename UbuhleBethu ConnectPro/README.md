# UbuhleBethu ConnectPro — Project Overview & Directory Index

Welcome to **UbuhleBethu ConnectPro**, an integrated enterprise solution for construction, quantity surveying, and site operations management in South Africa.

---

## Directory Navigation

- **[`Ubuhlebethu-Web/`](./Ubuhlebethu-Web)**
  Contains the backend ecosystem, including the **ASP.NET Core 10 Web Application (`UbuhleBethu-WebApp`)**, the **REST API (`UbuhlebethuConnectPro.Api`)**, the **Core domain models (`UbuhlebethuConnectPro.Core`)**, the **Infrastructure data access layer (`UbuhlebethuConnectPro.Infrastructure`)**, and the **Automated Integration Test Suite (`UbuhlebethuConnectPro.Tests`)**.
  - See [Web Documentation](./Ubuhlebethu-Web/UbuhleBethu-WebApp/README.md) for full setup, database configuration, role matrices, and operational guides.

- **[`Ubuhlebethu-Mobile/`](./Ubuhlebethu-Mobile)**
  Contains the **Native Android Application (`UbuhlebethuMApp`)** developed in Kotlin, featuring role-adaptive views for Field Workers (shift tracking), Site Managers (GPS photos, material delivery, team attendance), Office QS, and Administrators.
  - See [Mobile Documentation](./Ubuhlebethu-Mobile/README.md) for Android Studio setup, emulator guides, and forced password reset workflows.

---

## Quick Reference: Running the System

### 1. Start the API
```powershell
cd "Ubuhlebethu-Web/UbuhlebethuConnectPro.Api"
dotnet run --launch-profile https
```
Endpoints: `https://localhost:7031` / `http://0.0.0.0:5025`

### 2. Start the Web Dashboard
```powershell
cd "Ubuhlebethu-Web/UbuhleBethu-WebApp"
dotnet run
```
Browser URL: `https://localhost:7139`

### 3. Start the Android App
Open `Ubuhlebethu-Mobile/UbuhlebethuMApp` in **Android Studio**, sync Gradle, and run on an Android 11+ emulator or physical phone.

---

## Default Seeded Accounts

| Role | Username / Email | Password | Primary Interface |
| **System Admin** | `admin` | `St10461617` | `/Admin/Index` (User & Role Management, Audit Logs) |
| **Manager** | `manager` | `St10461617` | `/Dashboard/Management`, `/Manager/ReviewQuotes`, & `/ProjectManagement` |
| **Office / QS** | `office` | `St10461617` | `/Dashboard/Index` |
| **Client** | `client` | `St10461617` | `/Dashboard/ClientPortal` |
| **Site Manager** | `site` | `St10461617` | `/Dashboard/Index` |
| **Field Worker** | `worker@ubuhlebethu.co.za` | `FieldPass2026` | Android Clock-In Screen |

---

*For full architectural blueprints, API documentation, and security workflows, refer to the [Root README.md](../README.md).*
