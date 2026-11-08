# UbuhleBethu ConnectPro — Web Application & Backend API Documentation

[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)](#technology-stack)
[![Architecture](https://img.shields.io/badge/Architecture-ASP.NET%20Core%20MVC%20%2B%20Web%20API-teal.svg)](#architecture-overview)
[![Database](https://img.shields.io/badge/PostgreSQL-Neon%20Cloud-green.svg)](#database--orm-configuration)
[![Auth](https://img.shields.io/badge/Auth-Identity%20%2B%20JWT%20Bearer-red.svg)](#security--authentication)

This document provides in-depth technical and operational documentation for the **UbuhleBethu ConnectPro Web Application (`UbuhleBethu-WebApp`)** and its companion **Backend REST API (`UbuhlebethuConnectPro.Api`)**.

---

## Table of Contents

1. [Overview & Business Scope](#overview--business-scope)
2. [Architecture Overview](#architecture-overview)
3. [User Roles & Authorization Matrix](#user-roles--authorization-matrix)
4. [Functional Modules & Page Workflows](#functional-modules--page-workflows)
   - [1. User Management & Administration (`AdminController`)](#1-user-management--administration-admincontroller)
   - [2. Manager Review Quotes (`ManagerController`)](#2-manager-review-quotes-managercontroller)
   - [3. Dedicated Project Management & Deletion (`ProjectManagementController`)](#3-dedicated-project-management--deletion-projectmanagementcontroller)
   - [4. Office & Quantity Surveying (`OfficeController` & `DashboardController`)](#4-office--quantity-surveying-officecontroller--dashboardcontroller)
   - [5. Client Portal & Quotation Acceptance (`QuotationController`)](#5-client-portal--quotation-acceptance-quotationcontroller)
   - [6. Multi-Model AI Assistant Dock](#6-multi-model-ai-assistant-dock)
   - [7. Financial Invoicing & Currency Exchange](#7-financial-invoicing--currency-exchange)
5. [Backend REST API & Swagger (`UbuhlebethuConnectPro.Api`)](#backend-rest-api--swagger-ubuhlebethuconnectproapi)
6. [Database & ORM Configuration](#database--orm-configuration)
7. [Running the Application (Step-by-Step)](#running-the-application-step-by-step)
8. [Configuration & Environment Variables](#configuration--environment-variables)
9. [Automated Tests & Quality Verification](#automated-tests--quality-verification)

---

## Overview & Business Scope

**UbuhleBethu ConnectPro** is an enterprise construction and quantity surveying management system developed to eliminate workflow fragmentation in the building lifecycle. It coordinates quotation requests, quantity estimations, digital contract sign-offs, synchronized billing, site task reporting, and administrative governance.

---

## Architecture Overview

The web solution comprises five cleanly decoupled .NET projects:

```
Ubuhlebethu-Web/
├── UbuhlebethuConnectPro.Api/          # ASP.NET Core 10 Web API (REST endpoints)
├── UbuhleBethu-WebApp/                 # ASP.NET Core 10 MVC Web Frontend
├── UbuhlebethuConnectPro.Core/         # Domain Models, Interfaces, Enums, DTOs
├── UbuhlebethuConnectPro.Infrastructure/# Repositories, Database Context, S3, Email, AI
└── UbuhlebethuConnectPro.Tests/        # xUnit Integration and Unit Test Suite
```

### Communication Flow:
- The **Web Application** uses a strongly typed HTTP client (`ConnectProApiClient`) with a `JwtHandler` delegating handler to authenticate requests against the **API**.
- The **API** validates JWT Bearer tokens and handles database persistence via PostgreSQL (`ApplicationDbContext`).
- Real-time updates and metrics rely on EF Core query filters to recalculate order book values, progress, and balances whenever projects or quotes are modified or deleted.

---

## User Roles & Authorization Matrix

The web platform defines six primary user roles:

| Role Name | Portal URL | Target Responsibilities |
| :--- | :--- | :--- |
| **`Admin`** / **`SystemAdmin`** | `/Admin/Index` | Full identity and security administration: create users, combined "Edit User & Edit Roles", account deactivation/reactivation, permanent hard delete with audit reason, password reset with forced change on next login, and audit log inspection. *(Project deletion and project management tab are reserved solely for Managers).* |
| **`Manager`** / **`Management`** | `/Dashboard/Management` | Executive operations: dedicated **Review Quotes** workflow with KPI cards, avatar pills, and line-item approval, exclusive **Project Management** tab (single & master select-all project deletion for current and past projects), portfolio financial insights, AI strategic reporting. |
| **`Executive`** | `/Dashboard/Executive` | Read-only executive dashboard: macro-level corporate performance, portfolio health, order book values. |
| **`Office`** | `/Dashboard/Index` | Quantity Surveying & Administration: reviewing client quote requests, generating line-item estimates, issuing quotes, generating 1:1 synchronized invoices upon approval, tracking material usage and expenses. |
| **`Client`** | `/Dashboard/ClientPortal` | Customer self-service: submitting new quote requests, reviewing proposed quotes, digital signature and acceptance, tracking project progress %, inspecting invoices and payment receipts, live multi-currency conversion. |
| **`SiteManager`** | `/Dashboard/Index` | Field leadership: recording daily site progress %, uploading GPS-tagged progress photos, tracking on-site expenses and materials, managing team attendance records. |

---

## Functional Modules & Page Workflows

### 1. User Management & Administration (`AdminController`)
- **Combined Edit User & Roles**:
  - Located at `/Admin/EditUser/{id}`.
  - Combines personal details (Username, Email, Phone) and role assignment checkboxes into a single intuitive view. Admins can update a user's details and roles in one submission without switching between separate pages.
- **Permanent Hard Delete**:
  - Located at `/Admin/Delete/{id}`.
  - Requires confirmation and a mandatory audit reason.
  - Permanently removes the Identity user and role associations, while preserving an immutable record in the `AuditLogs` table. Admins cannot delete their own active account.
- **Password Reset & Forced Change**:
  - Admins can reset any user's password from `/Admin/Details/{id}`.
  - Sets the password to `TempPassword1!` and enables the `MustChangePasswordOnFirstLogin` flag.
  - On the user's next login (on either Web or Mobile), they are redirected to `/Account/ChangePassword` and cannot access the system until a new strong password is set.
- **Audit Logs (`/Admin/AuditLogs`)**:
  - Live filterable table of all security and administrative events (deactivations, hard deletes, password resets, role mutations, project deletions).
  - Exportable to PDF and CSV.

### 2. Manager Review Quotes (`ManagerController`)
- **URL**: `/Manager/ReviewQuotes`
- **Access**: Strictly restricted to `Manager`, `Management`, and `Executive`.
- **Key Capabilities**:
  - **Executive KPI Cards**: Real-time cards showing Total Quotes Volume, Pending Approval, Accepted Quotes, and Rejected Quotes.
  - **Interactive Search & Filtering**: Client-side filtering by client name, project type, and status pills.
  - **Modern Quote Table**: Client initials avatar pills, currency formatting, status badges, and action buttons.
  - **Inline Review Modal**: Instant modal displaying client scope, estimated cost, line-item breakdowns, and one-click **Sign Off / Approve** or **Reject** actions.

### 3. Dedicated Project Management & Deletion (`ProjectManagementController`)
- **URL**: `/ProjectManagement`
- **Access**: Strictly restricted to **`Manager`** and **`Management`** roles.
- **Key Capabilities**:
  - **View Current & Past Projects**: Segregates projects into Current (in progress) and Past (completed, closed, archived, 100% progress).
  - **Master Select-All Checkbox**: Header checkbox selects all currently visible projects.
  - **Bulk Deletion ("Delete Selected")**: Dynamically appears when one or more projects are selected. Opens a modal summarizing all selected projects and prompts for an audit justification reason.
  - **Single Project Deletion**: Row-level trash can button allows deleting individual projects.
  - **Automatic Metric Recalculation**: Deletion flags the project as deleted in PostgreSQL; all completion progress gauges, order book values, and invoices across the entire application adjust automatically.

### 4. Office & Quantity Surveying (`OfficeController` & `DashboardController`)
- **Pending Quotes Queue**: Office staff review submitted scopes, price materials, labor, and plant.
- **AI Estimation Drafting**: One-click AI cost estimation generates structured pricing recommendations based on historical rates.
- **Invoice Synchronization**: Once a quotation is approved and digitally signed by the client, the generated invoice amount **strictly matches the approved quotation amount (1:1)**.

### 5. Client Portal & Quotation Acceptance (`QuotationController`)
- **Request a Quote**: Submit project title, building type (Residential, Commercial, Industrial, Renovation), site address, target completion date, and budget.
- **Review & Digital Sign-Off**: Inspect the quote line items and submit digital acceptance.
- **Multi-Currency Live Feed**: Toggle prices between ZAR (R), USD ($), EUR (€), GBP (£), and others using real-time currency conversion rates.

### 6. Multi-Model AI Assistant Dock
- Embedded shrinkable chat dock available on all authenticated dashboard pages.
- **Shrink / Minimize Feature**: Users can collapse the chat window to prevent screen clutter.
- **Aligned Layout**: Fixed alignment for Budget, Status, and Expense metrics at the bottom of the chat panel.
- **Provider Routing**: Seamlessly routes questions between **Google Gemini 3.8 Flash**, **Groq Qwen 3.8**, and **NVIDIA Nemotron 3**.

---

## Backend REST API & Swagger (`UbuhlebethuConnectPro.Api`)

The backend API provides 21 REST controllers exposed with JWT Bearer authentication:

| Endpoint Route | Method | Description | Roles |
| :--- | :--- | :--- | :--- |
| `/api/v1/auth/login` | POST | Authenticates credentials and returns JWT Bearer token | Anonymous |
| `/api/v1/auth/change-password` | POST | Enforces mandatory password change | Authenticated |
| `/api/v1/dashboard/jobs` | GET | Returns all active construction projects | Authenticated |
| `/api/v1/dashboard/jobs/delete`| POST | Bulk deletion of selected projects with audit logging | Manager, Management |
| `/api/v1/dashboard/jobs/{id}` | DELETE | Deletes a single project | Manager, Management |
| `/api/v1/users/{id}/delete` | POST | Administrative permanent hard delete of a user | Admin, SystemAdmin |
| `/api/v1/quotes` | GET/POST| Manages quotation requests and line-item estimates | Office, Client, Manager |
| `/api/v1/invoices` | GET/POST| Manages invoices and payment tracking | Office, Client, Manager |
| `/api/v1/ai/chat` | POST | Multi-model AI assistant inference | Authenticated |

### Swagger Documentation:
- **Direct API Access**: `https://localhost:7031/swagger` (or `http://localhost:5025/swagger`).
- **WebApp Proxy**: Navigating to `https://localhost:7139/swagger` seamlessly forwards to the API's interactive Swagger UI.
- **Bearer Token Authorization**: In Swagger UI, click **Authorize** and enter:
  ```
  Bearer <your-jwt-token>
  ```

---

## Database & ORM Configuration

The system connects to a cloud-hosted PostgreSQL database on **Neon**:
- **Connection String**: Configured in `UbuhlebethuConnectPro.Api/appsettings.json`.
- **Automatic Migrations**: When the API starts up, `dbContext.Database.Migrate()` runs automatically in `Program.cs`.
- **Database Seeder**: Automatically creates roles (`Admin`, `Manager`, `Office`, `Client`, `SiteManager`, `Executive`) and default demo accounts with realistic quote data.
- **Soft Deletion & Query Filters**: Deleting jobs or users applies soft-delete flags with global EF Core query filters (`!p.IsDeleted`), ensuring deleted items immediately drop out of all analytics calculations without breaking foreign key referential integrity.

---

## Running the Application (Step-by-Step)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Internet connection (for PostgreSQL Neon database, S3 Backblaze, and AI APIs)

### Step 1: Start the Backend REST API
Open a terminal in the API folder:
```powershell
cd "UbuhleBethu ConnectPro/Ubuhlebethu-Web/UbuhlebethuConnectPro.Api"
dotnet run --launch-profile https
```
The API will start listening on:
- **`https://localhost:7031`** (HTTPS)
- **`http://localhost:5025`** (HTTP)

### Step 2: Start the WebApp Frontend
Open a second terminal window:
```powershell
cd "UbuhleBethu ConnectPro/Ubuhlebethu-Web/UbuhleBethu-WebApp"
dotnet run
```
The WebApp will launch at:
- **`https://localhost:7139`** (HTTPS)
- **`http://localhost:5138`** (HTTP)

### Step 3: Log In
Open your browser to `https://localhost:7139` and log in using any seeded demo account:
- **Admin**: `admin` / `St10461617`
- **Manager**: `manager` / `St10461617`
- **Office**: `office` / `St10461617`
- **Client**: `client` / `St10461617`

---

## Automated Tests & Quality Verification

Run the comprehensive test suite directly from the command line:
```powershell
dotnet test "UbuhleBethu ConnectPro/Ubuhlebethu-Web/UbuhlebethuConnectPro.Tests/UbuhlebethuConnectPro.Tests.csproj" -c Release
```

All 15 automated tests execute and pass:
- **Admin Role Mutation & Deletion Safeguards**
- **Auth & IDOR Anti-Tamper Security Verification**
- **Swagger UI & OpenAPI JSON Generation**
- **Multi-Currency Live Feed Conversion**

---

*UbuhleBethu ConnectPro &copy; 2026. Built with ASP.NET Core 10.*
