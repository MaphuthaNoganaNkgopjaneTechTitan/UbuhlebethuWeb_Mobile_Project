# UbuhleBethu ConnectPro — Enterprise Construction & Site Management Platform

[![Platform](https://img.shields.io/badge/Platform-.NET%2010%20%7C%20Android%20Kotlin-blue.svg)](#technology-stack)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%20(Neon)-green.svg)](#database--infrastructure)
[![AI Providers](https://img.shields.io/badge/AI-Google%20Gemini%20%7C%20Groq%20%7C%20NVIDIA-purple.svg)](#multi-model-ai-assistant)
[![Real-time](https://img.shields.io/badge/Real--time-SignalR%20Hub-orange.svg)](#7-live-web-dashboard-updates-signalr)
[![Build Status](https://img.shields.io/badge/Tests-33%20Passing%20(100%25)-brightgreen.svg)](#automated-testing--quality-assurance)
[![CI/CD](https://img.shields.io/badge/CI%2FCD-GitHub%20Actions%20%7C%20CodeQL-informational.svg)](#continuous-integration--continuous-deployment-cicd)

> **UbuhleBethu ConnectPro** is an end-to-end enterprise solution purpose-built for South African construction, quantity surveying (QS), and field site operations. It bridges the entire construction lifecycle: client quotation requests, automated and manual quantity surveying, client digital sign-offs, synchronized milestone invoicing, GPS-verified geofenced attendance and site logs, real-time live SignalR dashboard updates, multi-model AI assistant reasoning with function calling, Backblaze B2/S3 cloud photo storage, and role-governed administration.

---

## Table of Contents

1. [Executive Summary & Purpose](#executive-summary--purpose)
2. [Default Login Credentials for Testing](#default-login-credentials-for-testing)
3. [High-Level Architecture](#high-level-architecture)
4. [System Applications & Repository Structure](#system-applications--repository-structure)
5. [User Roles & Security Model](#user-roles--security-model)
6. [Core Features & Business Workflows](#core-features--business-workflows)
   - [1. Quotation & Milestone Invoicing Lifecycle](#1-quotation--milestone-invoicing-lifecycle)
   - [2. Project Management & Cascade Deletion](#2-project-management--cascade-deletion)
   - [3. Administrative Control & User Governance](#3-administrative-control--user-governance)
   - [4. Multi-Engine Robust AI Assistant & Cost Estimator](#4-multi-engine-robust-ai-assistant--cost-estimator)
   - [5. Geofencing & GPS Site Attendance](#5-geofencing--gps-site-attendance)
   - [6. Barcode Material Tracking](#6-barcode-material-tracking)
   - [7. Live Web Dashboard Updates (SignalR)](#7-live-web-dashboard-updates-signalr)
   - [8. Field Operations, S3 Storage & Mobile Sync](#8-field-operations-s3-storage--mobile-sync)
   - [9. Security, Observability & Rate Limiting](#9-security-observability--rate-limiting)
7. [Continuous Integration & Continuous Deployment (CI/CD)](#continuous-integration--continuous-deployment-cicd)
8. [Technology Stack](#technology-stack)
9. [Environment Configuration & Secrets](#environment-configuration--secrets)
10. [Step-by-Step Running Guide](#step-by-step-running-guide)
    - [Running the Backend REST API](#1-running-the-backend-rest-api)
    - [Running the ASP.NET Core WebApp](#2-running-the-aspnet-core-webapp)
    - [Running the Android Mobile App](#3-running-the-android-mobile-app)
    - [Accessing Swagger / OpenAPI Documentation](#4-accessing-swagger--openapi-documentation)
11. [Automated Testing & Quality Assurance](#automated-testing--quality-assurance)
12. [Troubleshooting & FAQ](#troubleshooting--faq)

---

## Default Login Credentials for Testing

The system automatically provisions test accounts in the PostgreSQL database with the exact test credentials below:

| Role | Username | Password | Email | System Permissions & Default Dashboard |
| :--- | :--- | :--- | :--- | :--- |
| **System Admin** | `admin` | `Admin123!` | `admin@connectpro.local` | System configuration, user creation with roles, deactivation, password resets, system health, and full audit logs (`/Admin/Index`). |
| **Manager** | `manager` | `Manager123!` | `manager@connectpro.local` | Executive KPI analytics, Project Management lifecycle, project deletion, quotation approval suite (`/Dashboard/Management`, `/Manager/ReviewQuotes`). |
| **Office / QS** | `office` | `Office123!` | `office@connectpro.local` | Quantity surveying queue, AI cost estimation, quote issuing, milestone invoice generation (`/Dashboard/Index`). |
| **Client** | `client` | `Client123!` | `client@connectpro.local` | Customer portal, digital quote approval, milestone invoices, project progress tracking, multi-currency display (`/Dashboard/ClientPortal`). |
| **Site Manager** | `site` | `Site123!` | `site@connectpro.local` | Construction site progress reporting, GPS-verified photo uploads, expense logging, team shifts (`/SiteReports/Project`, Android App). |
| **Field Worker** | `worker` | `Worker123!` | `worker@connectpro.local` | Native Android mobile clock-in/out with geofencing validation, barcode material scanning, daily log submission (Android App). |

> *Note: Legacy student credentials (e.g. `St10456109`, `St10461617`, etc.) also remain active and mapped to their respective roles with default password `St10461617`.*

---

## Executive Summary & Purpose

In the South African construction and civil engineering sector, communication gaps between site artisans, office quantity surveyors, corporate managers, and property clients result in costly project delays, unrecorded variations, budget creep, and compliance failures (e.g., NHBRC and SANS 10400 standards).

**UbuhleBethu ConnectPro** bridges all stakeholders into a unified, auditable platform:
- **Clients** submit architectural scopes, inspect estimates, digitally sign quotations, monitor real-time project progress bars, and download PDF tax invoices.
- **Quantity Surveyors & Office Staff** generate line-item estimates, verify NHBRC compliance, review variation orders, and dispatch quotes with live ZAR/USD/EUR currency support.
- **Site Managers & Field Workers** report daily progress, upload GPS-verified inspection photos, scan materials via barcodes, and clock in/out on site via the companion Android application.
- **Managers & Executives** oversee portfolio budgets, track executive KPIs, review and approve quotes in a dedicated review suite, and manage project lifecycles.
- **System Administrators** create users with role pre-selection, issue temporary password resets with forced password changes across both web and mobile, inspect audit trails, and monitor system health telemetry.

---

## High-Level Architecture

The platform follows a decoupled, clean enterprise architecture with separation of concerns:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             CLIENT LAYER                                    │
│  ┌────────────────────────────────────┐  ┌───────────────────────────────┐  │
│  │   UbuhleBethu WebApp (MVC)         │  │   UbuhlebethuMApp (Android)   │  │
│  │   - Razor Views + Bootstrap 5      │  │   - Kotlin, Jetpack, Coroutine│  │
│  │   - Real-time SignalR Listeners    │  │   - Material 3, Biometrics/GPS│  │
│  │   - Draggable AI Assistant Widget  │  │   - Barcode Scanner (ML Kit)  │  │
│  │   - Client Portal & Invoicing      │  │   - Offline Sync & Compression│  │
│  └──────────────────┬─────────────────┘  └───────────────┬───────────────┘  │
└─────────────────────┼────────────────────────────────────┼──────────────────┘
                      │ Cookie / Proxy                     │ Bearer JWT
                      ▼                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          GATEWAY / API LAYER                                │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                UbuhlebethuConnectPro.Api (.NET 10)                    │  │
│  │   - ASP.NET Core Web API with Dual Bindings (HTTP 5025 / HTTPS 7031)  │  │
│  │   - JWT Bearer Authentication & Strict RBAC Policies                  │  │
│  │   - SignalR SiteEventsHub (/hubs/site-events)                         │  │
│  │   - Rate Limiting (Fixed-Window IP / User Partitioning)               │  │
│  │   - Swashbuckle OpenAPI 2.x Documentation (/swagger)                  │  │
│  │   - Health Checks (/healthz) - Database, S3 Storage, Memory           │  │
│  │   - Serilog Structured Diagnostics Logging                            │  │
│  └──────────────────────────────────┬────────────────────────────────────┘  │
└─────────────────────────────────────┼───────────────────────────────────────┘
                                      │
                      ┌───────────────┴───────────────┐
                      ▼                               ▼
┌──────────────────────────────────────┐  ┌───────────────────────────────────┐
│     DOMAIN & BUSINESS LOGIC          │  │     EXTERNAL SERVICES & CLOUD     │
│  ┌────────────────────────────────┐  │  │  ┌─────────────────────────────┐  │
│  │ UbuhlebethuConnectPro.Core     │  │  │  │ PostgreSQL Neon Database    │  │
│  │  - Domain Models & Entities    │  │  │  │  - EF Core 10 Migrations    │  │
│  │  - Repository Interfaces       │  │  │  │  - Soft-delete Global Filter│  │
│  │  - Multi-Engine AI Abstractions│  │  │  │  - Geofence & Material Tables│ │
│  │  - AI Memory & Tool Calling    │  │  │  └─────────────────────────────┘  │
│  └────────────────┬───────────────┘  │  │  ┌─────────────────────────────┐  │
│                   ▼                  │  │  │ Backblaze B2 / S3 Storage   │  │
│  ┌────────────────────────────────┐  │  │  │  - Site photos & docs       │  │
│  │ UbuhlebethuConnectPro.Infrastr.│  │  │  │  - Local storage fallback   │  │
│  │  - EF Repositories & Migrations│  │  │  └─────────────────────────────┘  │
│  │  - Brevo SMTP Notifications    │  │  │  ┌─────────────────────────────┐  │
│  │  - Live Currency Exchange Feed │  │  │  │ Multi-Engine AI Gateway     │  │
│  │  - Milestone Invoicing Logic   │  │  │  │  - Google Gemini 3.8 Flash  │  │
│  │  - S3 / Local Storage Providers│  │  │  │  - Groq Qwen 3.8            │  │
│  └────────────────────────────────┘  │  │  │  - NVIDIA Nemotron 3        │  │
│                                      │  │  └─────────────────────────────┘  │
└──────────────────────────────────────┘  └───────────────────────────────────┘
```

---

## System Applications & Repository Structure

```
insy7315-2026-final-project-st10450570/
├── .github/
│   └── workflows/
│       ├── ci-cd.yml                            # GitHub Actions CI/CD Pipeline (Build, Test, APK, Deploy)
│       └── codeql.yml                           # GitHub CodeQL Static Application Security Testing (SAST)
├── README.md                                    # Root repository documentation (this file)
└── UbuhleBethu ConnectPro/
    ├── Ubuhlebethu-Web/                         # Web and Backend .NET Solution
    │   ├── UbuhleBethuConnectPro.Web.slnx       # Modern .NET solution entry point
    │   │
    │   ├── UbuhlebethuConnectPro.Api/           # Backend REST API (.NET 10)
    │   │   ├── Controllers/Api/                 # 22 Controllers (Auth, Jobs, Users, AI, Reports, Materials, etc.)
    │   │   ├── Health/                          # Health checks (Database, Storage, Memory)
    │   │   ├── Hubs/                            # SignalR Hubs (SiteEventsHub)
    │   │   ├── Middleware/                      # GlobalExceptionMiddleware, RateLimiting
    │   │   ├── Data/                            # DbSeeder & ProjectActivityStore
    │   │   ├── Program.cs                       # Kestrel pipeline, MigrationsAssembly & DI configuration
    │   │   └── appsettings.json                 # Connection strings, JWT options, S3 & AI secrets
    │   │
    │   ├── UbuhleBethu-WebApp/                  # ASP.NET Core MVC Web Application
    │   │   ├── Controllers/                     # MVC Controllers (Dashboard, Admin, Manager, SiteReports, etc.)
    │   │   ├── ApiClient/                       # ConnectProApiClient strongly-typed HTTP client
    │   │   ├── Views/                           # Razor Views & Components
    │   │   │   ├── Admin/                       # User management, CreateUser, EditUser, Health Dashboard
    │   │   │   ├── Dashboard/                   # Office, Details, ClientPortal (SignalR live updates)
    │   │   │   ├── Manager/                     # ReviewQuotes, Financial KPIs
    │   │   │   ├── ProjectManagement/           # Operations Project Lifecycle & Bulk Deletion
    │   │   │   ├── SiteReports/                 # Site inspection reports, photos & progress
    │   │   │   └── Shared/                      # Navigation, Layout, _ChatbotPartial, _LiveUpdatesPartial
    │   │   └── wwwroot/                         # CSS styling, JavaScript draggable AI assistant, jsPDF export
    │   │
    │   ├── UbuhlebethuConnectPro.Core/          # Domain Models, Interfaces, AI Architecture & Cost Estimator
    │   ├── UbuhlebethuConnectPro.Infrastructure/# EF Core ApplicationDbContext, Migrations, S3 Provider, Brevo SMTP
    │   └── UbuhlebethuConnectPro.Tests/         # 32 Automated Integration & Unit Tests (xUnit, Moq)
    │
    └── Ubuhlebethu-Mobile/                      # Native Android Application (Kotlin)
        ├── README.md                            # Mobile app specific documentation
        └── UbuhlebethuMApp/
            ├── app/src/main/java/               # Kotlin codebase
            │   ├── LoginActivity.kt             # Authentication & mandatory password change interceptor
            │   ├── ScannerActivity.kt           # ML Kit Barcode camera scanning
            │   ├── MainActivity.kt              # Responsive, auto-optimized role fragments
            │   ├── model/                       # Data models & API contracts
            │   ├── ui/                          # Role fragments (FieldWorker, SiteManager, etc.)
            │   ├── utils/                       # ImageCompressor & GeofenceUtils
            │   └── worker/                      # WorkManager background offline sync
            └── build.gradle.kts                 # Kotlin DSL build configuration (Gradle 9.4.1)
```

---

## User Roles & Security Model

The system enforces strict Role-Based Access Control (RBAC) backed by ASP.NET Core Identity. The table below outlines core permissions:

| Role Name | Description | Key Capabilities |
| :--- | :--- | :--- |
| **`Admin`** | System & Security Administrator | Create users with immediate role assignment, unified Edit User & Roles, account deactivation/reactivation, permanent hard delete with audit reason, password reset with forced first-login change, system health monitoring (`/Admin/Health`), and audit trail inspection. *(Project deletion is strictly reserved for Operations Management).* |
| **`Manager`** | Operations & Project Management | Exclusive access to `/ProjectManagement` (single & bulk cascade project deletion), Manager Review Quotes suite (executive KPIs, line items, sign-off), portfolio finance tracking, AI strategic analysis. |
| **`Office`** | Quantity Surveyor / Office Administrator | Quotation queue processing, AI drafting of estimates, quotation approval & issuing, 1:1 synchronized milestone invoice generation, material tracking, and job updates. |
| **`Client`** | Customer / Property Owner | Client portal access, quote request submission, digital quote sign-off/acceptance, milestone invoice inspection, payment receipt downloads, live ZAR/USD/EUR currency conversions. |
| **`SiteManager`** | Site Manager / Foreman | Construction site reporting, logging daily progress %, GPS-tagged photo evidence upload to S3, material delivery inspection, petty cash/expense tracking, site team shift management. |
| **`FieldWorker`** | Field Technician / Artisan (Mobile) | Native mobile shift management: start/finish shift (clock-in/clock-out) with timestamp and geofencing validation, barcode material scanning, daily log submission. |

---

## Core Features & Business Workflows

### 1. Quotation & Milestone Invoicing Lifecycle
1. **Quote Request**: A `Client` specifies the building type, location, estimated budget, and scope.
2. **Estimation & Pricing**: An `Office` QS reviews the request, calculates material/labor costs (optionally using the AI cost estimator), and formalizes the quote.
3. **Manager Review**: The `Manager` accesses `/Manager/ReviewQuotes` to review the quote queue against executive KPIs, examine line items, and sign off.
4. **Client Digital Acceptance**: The client reviews the approved quote in `/Client/ReviewQuote` and submits their digital sign-off.
5. **Milestone Invoicing**: Invoices support phased progress billing:
   - **25% Deposit** upon contract signing.
   - **50% Mid-Way Progress** upon structural milestone reached.
   - **100% Final Completion** upon final handover.
6. **Printable Tax Invoices**: Full South African compliant tax invoice view (`/Invoice/Print/{id}`) with company header, VAT calculations, and browser print/PDF export.

### 2. Project Management & Cascade Deletion
- **Dedicated Tab (`/ProjectManagement`)**: Accessible strictly to **`Manager`** roles.
- **Status Segmentation**: Filter between **All**, **Current**, and **Past** projects (based on completion status or 100% progress).
- **Master Select-All & Bulk Delete**: Select all visible projects with one click or choose individual items; a prominent "Delete Selected (N)" action activates.
- **Single-Item Delete**: Dedicated row-level delete buttons with instant confirmation modals.
- **Audit Logging**: Every deletion requires a recorded justification reason saved into the audit logs.
- **Automatic Metric Recalculation**: Thanks to global database query filters, deleting projects immediately updates progress percentages, active job counts, invoiced totals, and outstanding order book balances across every dashboard in real time.

### 3. Administrative Control & User Governance
- **Role Assignment on Creation**: Admins select access roles directly during user creation (`/Admin/CreateUser`).
- **Unified User Editing**: Edit User Profile and Edit Roles are unified into one seamless, clean action (`/Admin/EditUser`).
- **Duplicate Token Prevention**: Forms utilize standard single anti-forgery tokens, eliminating HTTP 400 Bad Request issues.
- **Graceful Error Handling**: Custom error pages (`/Account/Error?statusCode=...`) display branded diagnostic guidance with recovery buttons rather than raw browser error screens.
- **Password Reset with Forced First-Login Change**: When an admin resets a user password, the system generates a temporary credential (`TempPassword1!`). On both Web and Mobile, the user is intercepted upon login and forced to enter and confirm a new, strong password before accessing any system features.

### 4. Multi-Engine Robust AI Assistant & Cost Estimator
The AI Assistant has been upgraded from a basic Q&A bot into an intelligent, system-aware agent:
- **Repositionable / Draggable Toggle**: The webapp AI toggle button can be clicked and dragged to any position on the screen to prevent blocking navigation or content. Clicking it expands a docked chat window anchored to the bottom right.
- **System-Aware Step-by-Step Guidance**: The assistant contains deep knowledge of the platform's workflows and tells users the exact step-by-step procedures to accomplish tasks.
- **Role Capability Advisory**: Informs the user whether their current role can execute an action and explicitly details which roles have the authority (e.g. *"Only Managers can delete projects under /ProjectManagement"*).
- **Database & API Tool Calling (AIToolExecutor)**: Securely retrieves real-time database records including project metrics, expenses, line-item budgets, and recent field reports without exposing sensitive credentials.
- **Construction Cost Estimator**: Features South African construction cost models based on square meters, building category (Residential, Commercial, Industrial), finish quality (Standard, Luxury, High-End), and province adjustments.
- **User Memory Persistence (`UserAIMemoryService`)**: Retains short-term context and conversation history per user session for natural multi-turn conversations.
- **Multi-Model Fallback**: Automatically tries **Google Gemini 3.8 Flash**, falling back to **Groq Qwen 3.8** or **NVIDIA Nemotron 3** if rate limits or network issues occur.

### 5. Geofencing & GPS Site Attendance
- Projects can be configured with GPS coordinates (`Latitude`, `Longitude`) and a `GeofenceRadiusMeters` threshold (default 300 meters).
- When field workers start a shift on mobile, the app captures device GPS and computes the Haversine distance to the site.
- If outside the site boundary, the attendance record is flagged as `IsOffSite` with `DistanceFromSiteMeters` recorded for managerial audit.
- Site managers can review off-site attendance flags on the web dashboard before approving hours.

### 6. Barcode Material Tracking
- Full barcode inventory management backed by the `Materials` database table.
- Mobile field workers and site managers scan material delivery barcodes using the on-device camera powered by Google ML Kit (`ScannerActivity`).
- Scanned items are checked against the project allocation, logging registered quantities, unit costs, and receiving timestamps.

### 7. Live Web Dashboard Updates (SignalR)
- Lightweight real-time hub (`SiteEventsHub`) mapped at `/hubs/site-events`.
- When field workers upload daily logs, photos, or scan materials on mobile, the API broadcasts real-time events (`SiteReportAdded`, `SitePhotoUploaded`, `MilestoneProgressUpdated`).
- The **Manager Details page** (`/Dashboard/Details/{id}`) and **Client Portal** (`/Dashboard/ClientPortal`) automatically update progress bars, recalculate status metrics, and insert new inspection photos with smooth animations—**no manual page refresh required**.

### 8. Field Operations, S3 Storage & Mobile Sync
- **Backblaze B2 / S3 Cloud Storage**: Progress photos and compliance documents are stored in S3 cloud storage (`S3StorageService`) with pre-signed URL retrieval and local storage fallback (`LocalStorageService`).
- **Client-Side Image Compression**: Mobile app compresses high-resolution camera photos (`ImageCompressor.kt`) prior to upload to conserve mobile bandwidth.
- **Auto-Optimized Mobile UI**: Android layouts dynamically adapt to screen dimensions and densities, preventing header and button overlap across various phone form factors.
- **Offline Sync (WorkManager)**: Shift logs and reports recorded with low connectivity are queued locally and synced automatically once internet access resumes.

### 9. Security, Observability & Rate Limiting
- **API Rate Limiting (`AspNetCoreRateLimiting`)**:
  - Auth endpoints (`/api/v1/auth/login`, `/api/v1/auth/register`): 5 requests/minute per IP address to block brute force attacks.
  - AI endpoints (`/api/v1/ai/*`): 15 requests/minute per user/IP to prevent token exhaustion.
  - Standard HTTP 429 response with `Retry-After` headers.
- **Health Checks Endpoint (`/healthz`)**:
  - Monitors PostgreSQL database connectivity, S3 storage reachability, and memory consumption.
  - Accessible via `/healthz` (JSON) or the Admin Web UI at `/Admin/Health` with interactive status indicators, response latency, and system uptime.

---

## Continuous Integration & Continuous Deployment (CI/CD)

The project includes an enterprise-grade CI/CD pipeline powered by GitHub Actions:

### 1. Main CI/CD Workflow ([`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml))
Triggers on pushes to `main` / `develop`, pull requests, and manual dispatch:
- **`dotnet-ci` Job**:
  - Sets up .NET 10 SDK on Ubuntu.
  - Restores dependencies and builds the entire solution (`UbuhleBethuConnectPro.Web.slnx`).
  - Executes all 32 unit and integration tests with detailed trx and coverage reporting.
  - Publishes web deployment packages for `UbuhlebethuConnectPro.Api` and `UbuhleBethu-WebApp`.
- **`android-ci` Job**:
  - Sets up Java 17 (Temurin) and Gradle 9.4.1.
  - Runs mobile unit tests (`./gradlew testDebugUnitTest`).
  - Assembles the debug APK (`./gradlew assembleDebug`) and archives the `.apk` as a downloadable artifact.
- **`security-audit` Job**:
  - Scans NuGet packages for known Common Vulnerabilities and Exposures (CVEs) using `dotnet list package --vulnerable`.
- **`deploy-production` Job**:
  - Triggers on pushes to `main` or manual workflow dispatch.
  - Deploys the published WebApp and API packages to the cloud hosting provider (`runasp.net`) via WebDeploy / MSDeploy.
  - Configured via GitHub repository secrets: `WEBDEPLOY_USERNAME`, `WEBDEPLOY_PASSWORD`, `WEBDEPLOY_SERVER`, `WEBDEPLOY_SITENAME`.

### 2. CodeQL SAST Security Workflow ([`.github/workflows/codeql.yml`](.github/workflows/codeql.yml))
- Runs GitHub CodeQL Static Application Security Testing on C# code to scan for injection vulnerabilities, security misconfigurations, and privilege escalations on every pull request.

---

## Technology Stack

### Backend & Web (.NET 10)
- **Framework**: ASP.NET Core 10 (Web API + MVC)
- **Language**: C# 13 / .NET 10 SDK
- **Database ORM**: Entity Framework Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Database Engine**: PostgreSQL (Neon Cloud)
- **Real-Time Engine**: ASP.NET Core SignalR
- **Authentication**: ASP.NET Core Identity + JWT Bearer Tokens (`System.IdentityModel.Tokens.Jwt`)
- **Rate Limiting**: `Microsoft.AspNetCore.RateLimiting`
- **Cloud Storage**: AWS S3 SDK (`AWSSDK.S3`) configured for Backblaze B2
- **API Documentation**: Swashbuckle OpenAPI 2.x (`Swashbuckle.AspNetCore` 10.3)
- **Logging**: Serilog with Daily File Rolling and Console Sinks
- **PDF & Export**: jsPDF + AutoTable client-side export, server-side CSV streaming

### Mobile (Android)
- **Language**: Kotlin 2.0+
- **Minimum SDK**: API 25 (Android 7.1 Nougat)
- **Target / Compile SDK**: API 35 / 37
- **UI Architecture**: Single-Activity Architecture, Jetpack Navigation, Material Design 3, View Binding
- **Camera & Barcode**: Google ML Kit Barcode Scanning + CameraX
- **Background Sync**: Android Jetpack WorkManager
- **Asynchrony**: Kotlin Coroutines + Flow

---

## Environment Configuration & Secrets

Configuration files are located in `UbuhlebethuConnectPro.Api/appsettings.json` and `UbuhleBethu-WebApp/appsettings.json`. For local development, sensitive values can be overridden in `appsettings.Local.json` (git-ignored) or environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-neon-host.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=your_password;SslMode=Require;"
  },
  "Jwt": {
    "Key": "uGOiXoITACM6JxFkWwy8ySzwYLdPuFRmpBtVpQeynpixD6rZ/+8M0qqipg2AD6qY",
    "Issuer": "UbuhlebethuConnectPro",
    "Audience": "UbuhlebethuConnectPro.Clients",
    "ExpiryMinutes": 60
  },
  "Api": {
    "BaseUrl": "https://localhost:7031"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-3.8-flash"
  },
  "Groq": {
    "ApiKey": "YOUR_GROQ_API_KEY",
    "Model": "qwen/qwen3.8-27b"
  },
  "Nvidia": {
    "ApiKey": "YOUR_NVIDIA_API_KEY",
    "Model": "nvidia/nemotron-3-super-120b-a12b"
  },
  "Storage": {
    "Provider": "s3",
    "ServiceUrl": "https://s3.us-east-005.backblazeb2.com",
    "Bucket": "ubuhlebethu",
    "AccessKeyId": "YOUR_B2_KEY_ID",
    "SecretAccessKey": "YOUR_B2_SECRET",
    "MaxPhotoBytes": 5242880
  }
}
```

---

## Step-by-Step Running Guide

### 1. Running the Backend REST API
Open a terminal in the API project folder:
```powershell
cd "UbuhleBethu ConnectPro/Ubuhlebethu-Web/UbuhlebethuConnectPro.Api"
dotnet restore
dotnet run --launch-profile https
```
- **Listening Endpoints**: `https://localhost:7031` and `http://0.0.0.0:5025`.
- **Database Migrations & Seed**: Automatically discovered and applied on startup via `MigrationsAssembly`.

### 2. Running the ASP.NET Core WebApp
In a second terminal window:
```powershell
cd "UbuhleBethu ConnectPro/Ubuhlebethu-Web/UbuhleBethu-WebApp"
dotnet restore
dotnet run
```
- Open your browser at: **`https://localhost:7139`** (or `http://localhost:5138`).

### 3. Running the Android Mobile App
1. Open **Android Studio**.
2. Select **Open an Existing Project** and choose `UbuhleBethu ConnectPro/Ubuhlebethu-Mobile/UbuhlebethuMApp`.
3. Allow Gradle to sync dependencies (Gradle 9.4.1).
4. Select an Android Virtual Device (AVD running API 28+) or connect a physical phone via USB.
5. Click **Run 'app'** (`Shift + F10`).
   - *To compile via command line*:
     ```powershell
     cd "UbuhleBethu ConnectPro/Ubuhlebethu-Mobile/UbuhlebethuMApp"
     .\gradlew.bat assembleDebug
     ```

### 4. Accessing Swagger / OpenAPI Documentation
Swagger UI is active and accessible via either:
- **API Server Direct**: `https://localhost:7031/swagger` (or `http://localhost:5025/swagger`)
- **WebApp Proxy**: `https://localhost:7139/swagger` (automatically forwards to API documentation)

---

## Automated Testing & Quality Assurance

The solution includes an extensive suite of 32 automated integration and unit tests under `UbuhlebethuConnectPro.Tests`:
- **Identity & IDOR Tests**: Asserts that unauthorized clients cannot inspect or tamper with another client's quotation or invoice.
- **Admin Privilege & Role Mutation Tests**: Validates user governance, role creation, single antiforgery token rendering, and user hard delete constraints.
- **Database Schema & Geofencing Tests**: Confirms column existence and geospatial queries for `Projects`, `AttendanceRecords`, and `SiteReports`.
- **Milestone Invoicing Tests**: Asserts 25% / 50% / 100% deposit calculations, invoice totals, and printable tax invoice view models.
- **AI Assistant Robustness Tests**: Verifies multi-engine fallbacks, tool calling execution, user memory persistence, role permission evaluation, and QS cost estimation.
- **Security & Health Check Tests**: Asserts rate limiter IP partitioning on `/api/v1/auth/login`, token bucket limits on AI endpoints, and `/healthz` JSON telemetry.
- **Swagger Generation Tests**: Confirms OpenAPI document compilation, unique schemaId resolution, and root route redirects.

To execute the test suite:
```powershell
dotnet test "UbuhleBethu ConnectPro/Ubuhlebethu-Web/UbuhlebethuConnectPro.Tests/UbuhlebethuConnectPro.Tests.csproj" -c Release
```
**Results**: `Passed: 33, Failed: 0, Skipped: 0 (100% Success)`.

---

## Troubleshooting & FAQ

1. **Database Exception: `column p.GeofenceRadiusMeters does not exist`?**
   - Ensure the API is running with the latest configuration. `UbuhlebethuConnectPro.Api` has been configured with `b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)` so EF Core automatically discovers and executes all pending infrastructure migrations on startup.
2. **HTTP 400 Bad Request on Admin Edit User?**
   - This has been resolved. Forms now use a single antiforgery token, client-side validation prevents empty role submission, and status code re-execution displays informative guidance rather than raw browser error screens.
3. **AI Assistant is blocking elements on screen?**
   - The AI Assistant toggle button is draggable. Simply click and drag it to any preferred position on your screen; opening the chat dock will keep it anchored neatly at the bottom right.
4. **"The user must change their password on first login"?**
   - If an admin reset the user's password, the account is flagged for mandatory password change. Enter the temporary password (`TempPassword1!`), and you will be automatically prompted to set a new password.
5. **Swagger returns 404 or connection refused?**
   - Ensure the API project is running on `https://localhost:7031` or `http://localhost:5025`. The WebApp automatically proxies `/swagger` requests to this address.

---

*UbuhleBethu ConnectPro &copy; 2026. Developed for INSY7315 Final Project.*
