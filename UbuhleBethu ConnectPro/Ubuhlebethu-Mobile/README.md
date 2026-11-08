# UbuhlebethuMApp — Native Android Mobile Application

[![Platform](https://img.shields.io/badge/Platform-Android-green.svg)](#technology-stack)
[![Language](https://img.shields.io/badge/Language-Kotlin%202.0-purple.svg)](#technology-stack)
[![Min SDK](https://img.shields.io/badge/Min%20SDK-25%20(Android%207.1)-orange.svg)](#prerequisites)
[![Target SDK](https://img.shields.io/badge/Target%20SDK-35%20%7C%20Compile%2037-blue.svg)](#prerequisites)

The **UbuhlebethuMApp** is the native Android companion mobile client for **UbuhleBethu ConnectPro**. It extends construction management, quantity surveying, and field site operations directly to smartphones and ruggedized site tablets. The application features a dynamic, role-adaptive interface that adjusts layouts, navigation, and workflows based on the logged-in user's role.

---

## Table of Contents

1. [Overview & Role-Adaptive Architecture](#overview--role-adaptive-architecture)
2. [User Roles & Mobile Capabilities](#user-roles--mobile-capabilities)
   - [Field Worker](#1-field-worker)
   - [Site Manager](#2-site-manager)
   - [Office / Quantity Surveyor](#3-office--quantity-surveyor)
   - [System Administrator](#4-system-administrator)
3. [Security & Authentication Workflows](#security--authentication-workflows)
   - [Forced Password Reset on First Login](#forced-password-reset-on-first-login)
4. [Technology Stack & Dependencies](#technology-stack--dependencies)
5. [Project Architecture & Package Structure](#project-architecture--package-structure)
6. [Prerequisites & Build Instructions](#prerequisites--build-instructions)
7. [Running on Emulators & Physical Devices](#running-on-emulators--physical-devices)
8. [Backend API Connectivity](#backend-api-connectivity)
9. [Demo Credentials & Testing Accounts](#demo-credentials--testing-accounts)

---

## Overview & Role-Adaptive Architecture

In construction environments, mobile users have vastly different responsibilities:
- A **Field Artisan** requires a high-contrast, distraction-free screen to clock in and clock out of shifts without navigating complex menus.
- A **Site Manager** requires tools to log daily work, capture GPS-verified site photos, scan materials, and inspect team attendance.
- An **Office QS** or **Admin** needs portable oversight to inspect quotes, approve requests, and manage access.

**UbuhlebethuMApp** dynamically restructures its navigation bar and screen containers upon login to deliver an optimized interface tailored strictly to that role.

---

## User Roles & Mobile Capabilities

### 1. Field Worker
- **Simplified Single-Screen Interface**: Free of bottom navigation clutter.
- **Shift Clock-In & Clock-Out**: Tap to start or finish a shift; records exact UTC and local timestamps with site geolocation.
- **Attendance History**: Visual list of past shifts with manager verification status (Pending, Approved).
- **Daily Safety Notices**: Urgent site notices and hazard bulletins broadcast by the site foreman.

### 2. Site Manager
- **Daily Progress Logging**: Update completion percentages on assigned construction jobs.
- **GPS-Tagged Photo Evidence**: Capture and upload high-resolution progress photos tagged with location coordinates directly to the cloud S3 storage bucket.
- **Material Receiving & Scanning**: Record material deliveries, supplier notes, and quantities.
- **Site Expense Tracking**: Log petty cash slips and site expenses directly from the field.
- **Team Attendance Oversight**: Review and approve field worker clock-in records.
- **AI Estimation Query**: Instant estimation suggestions based on scanned materials.

### 3. Office / Quantity Surveyor
- **Quotation Suggestions**: Review submitted project scopes and draft line-item estimates on the go.
- **Approve / Reject Quotes**: Expedite customer quotation sign-offs from mobile devices.
- **Invoice Tracking**: Verify customer billing status and payment confirmations.

### 4. System Administrator
- **User Governance**: Inspect user accounts, assign roles, and issue new credentials.
- **Audit Monitoring**: Portable view of security logs and system alerts.

---

## Security & Authentication Workflows

### Forced Password Reset on First Login
To maintain strict enterprise security compliance:
1. When an administrator resets a user's password via the Web Dashboard, a temporary password (`TempPassword1!`) is generated, and the account is flagged for mandatory password change.
2. When the user logs in to **UbuhlebethuMApp** with the temporary password, the authentication handler detects the requirement.
3. The app immediately intercepts navigation and presents the **Change Password Modal**:
   - The user must enter a new, strong password (confirming password complexity).
   - Once successfully updated, the token is refreshed, the temporary flag is cleared, and the user is redirected to their role's dashboard.
   - Access to any functional screens is blocked until the password is changed.

---

## Technology Stack & Dependencies

- **Language**: Kotlin 2.0+ (JVM 17 bytecode)
- **Minimum SDK**: API 25 (Android 7.1 Nougat)
- **Target SDK**: API 35 (Android 15)
- **Compile SDK**: API 37
- **UI Architecture**: Material Design 3 Components, Android Fragments, View Binding
- **Navigation**: Jetpack Navigation Component with BottomNavigationView
- **Networking & API**: Retrofit 2 + OkHttp3, Moshi JSON Converter, JWT Bearer Token Interceptor
- **Asynchrony**: Kotlin Coroutines (`Dispatchers.IO`, `Dispatchers.Main`) + StateFlow / LiveData
- **Build System**: Gradle 8.x with Kotlin DSL (`build.gradle.kts`) and Version Catalog (`libs.versions.toml`)

---

## Project Architecture & Package Structure

```
UbuhlebethuMApp/app/src/main/java/com/example/ubuhlebethumapp/
│
├── LoginActivity.kt              # Role selector, demo autofill, and login interceptor
├── ChangePasswordActivity.kt     # Mandatory first-login password reset screen
├── MainActivity.kt               # Dynamic bottom navigation host and role coordinator
│
├── model/                        # Data contracts and API DTOs
│   ├── UserRole.kt               # Enum: FieldWorker, SiteManager, OfficeQS, SystemAdmin
│   ├── AttendanceRecord.kt       # Clock-in / clock-out timestamp and GPS model
│   ├── ExpenseItem.kt            # On-site expense itemization
│   ├── ProjectEstimate.kt        # Quantity surveying scope and estimation model
│   └── ChangePasswordRequest.kt  # Contract for forced password change
│
└── ui/                           # UI Fragments, Adapters, and ViewHolders
    ├── FieldWorkerFragment.kt    # Dedicated worker clock-in interface
    ├── SiteManagerFragment.kt    # Progress logging, photo capture, and team attendance
    ├── OfficeQSFragment.kt       # Quotation reviewing and approvals
    ├── ManagementFragment.kt     # Administrative overview and role assignment
    ├── AttendanceAdapter.kt      # RecyclerView adapter for attendance records
    └── ExpensesAdapter.kt        # RecyclerView adapter for site expense items
```

---

## Prerequisites & Build Instructions

### Prerequisites
- **Android Studio** (Koala, Ladybug, or recent stable release).
- **Java Development Kit (JDK)**: JDK 17.
- **Android SDK**: Platforms 25 through 35 installed via Android Studio SDK Manager.

### Step-by-Step Build:
1. Launch **Android Studio**.
2. Select **File > Open** and browse to:
   ```
   UbuhleBethu ConnectPro/Ubuhlebethu-Mobile/UbuhlebethuMApp
   ```
3. Allow Gradle to sync and download all dependencies defined in `gradle/libs.versions.toml`.
4. Verify build configuration by selecting **Build > Make Project** (`Ctrl + F9` or `Cmd + F9`).

---

## Running on Emulators & Physical Devices

### Option A: Android Virtual Device (AVD Emulator)
1. In Android Studio, open the **Device Manager** (`Tools > Device Manager`).
2. Create or select a virtual device running **Android 11.0 (API 30)** or higher (Pixel 7 / 8 recommended).
3. Select the `app` run configuration in the top toolbar.
4. Click **Run** (`Shift + F10`).

### Option B: Physical Device via USB Debugging
1. On your Android phone, enable **Developer Options** and turn on **USB Debugging**.
2. Connect your phone via USB.
3. Accept the USB debugging authorization prompt on your phone screen.
4. Select your physical phone in Android Studio's target device dropdown and click **Run**.

---

## Backend API Connectivity

When testing against a locally running instance of `UbuhlebethuConnectPro.Api`:
- **Android Emulator**: The emulator accesses the host machine's localhost via **`http://10.0.2.2:5025`**.
- **Physical Device**: Ensure your phone is connected to the same Wi-Fi network as your development workstation, and use your PC's local LAN IP (e.g., `http://192.168.1.100:5025`).

---

## Demo Credentials & Testing Accounts

The login screen provides one-tap role selection chips that automatically populate demo credentials:

| Role Chip | Email / Username | Default Password | Initial View |
| :--- | :--- | :--- | :--- |
| **Field Worker** | `worker@ubuhlebethu.co.za` | `FieldPass2026` | Clock-In / Shift Tracker |
| **Site Manager** | `sitemanager@ubuhlebethu.co.za` | `SitePass2026` | Site Hub, Photos, Expenses |
| **Office / QS** | `qs@ubuhlebethu.co.za` | `QSPass2026` | Quotation & Estimation Queue |
| **System Admin** | `admin@ubuhlebethu.co.za` | `AdminPass2026` | User Administration & Logs |

*(Note: If an administrator resets your password to `TempPassword1!`, the app will require you to change it upon submitting the credentials above).*

---

*UbuhlebethuMApp &copy; 2026. Native Android Client for UbuhleBethu ConnectPro.*
