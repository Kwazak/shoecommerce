# Karya Jaya 1978 — Indonesian Footwear E-Commerce (B2C & B2B)

A production-grade **.NET 10 ASP.NET Core Razor Pages** e-commerce platform for an Indonesian footwear manufacturer (**Pabrik Sepatu Karya Jaya, Est. 1978**) serving both retail consumers (**B2C**) and regional wholesale shoe shops (**B2B**).

---

## 1. Project Overview & Design Philosophy

- **Dual-Channel Commerce:**
  - **B2C Retail (`CustomerB2C` & Guest Checkout):** Direct-from-factory pricing (`Rp 65.000` – `Rp 260.000`), regular courier shipping, Midtrans Snap online payments, and Cash on Delivery (`COD`).
  - **B2B Wholesale (`PartnerB2B`):** Self-service shop registration with Admin verification (`IsB2BApproved`), automatic tiered *lusinan* discounts (`6–11`, `12–23`, `24+` pairs), a multi-variant Size-Series Accounting Ledger Matrix (`38–44`), Land Freight (*Ekspedisi Kargo Truk*), and **Tempo Mitra B2B (14 Hari)** credit terms.
- **1970s–1980s Indonesian Print-Era Aesthetic:**
  - **Zero Bootstrap / Zero jQuery / Zero Generic SaaS UI:** Built with Tailwind CSS (CDN with custom vintage tokens), custom print-era CSS (`wwwroot/css/vintage.css`), and modular Vanilla JavaScript (`wwwroot/js/storefront.js`, `wwwroot/js/admin-charts.js`).
  - **Visual Tokens:** Aged newsprint (`#F4EFE6`), warm cardstock (`#EAE0D0`), saddle leather (`#4A2E1B`), terracotta orange (`#C85A32`), bottle green (`#2E4A3E`), and press ink (`#231F20`), paired with `Fraunces`, `Courier Prime`, and `Plus Jakarta Sans`.
  - **Tactile Print Components:** Hard ink offset shadows (`4px 4px 0px #231F20`), double-ruled borders, subtle SVG `feTurbulence` paper grain, radial halftone dot badges, dynamic inline SVG rubber stamps, slide-out *Laci Kasir* cart drawer, and inline SVG footwear illustrations (`RetroSvgHelper`) with hybrid product photography fallback.

---

## 2. Tech Stack Summary

| Layer | Technology / Implementation |
| --- | --- |
| **Runtime & Web Framework** | .NET 10 (`net10.0`), ASP.NET Core Razor Pages + Minimal APIs |
| **Database & ORM** | Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.Sqlite` default; `Npgsql.EntityFrameworkCore.PostgreSQL` ready) |
| **Caching** | .NET `Microsoft.Extensions.Caching.Hybrid` (`HybridCache`) with tag-based invalidation (`"catalog"`, `"wholesale-tiers"`) |
| **Authentication & RBAC** | Custom `User` entity with `PasswordHasher<User>` & Cookie Authentication (`Admin`, `PartnerB2B`, `CustomerB2C`) |
| **Payment Gateway** | Midtrans Snap API (`/snap/v1/transactions`) + `POST /api/payments/webhook` (SHA-512 verification) + local dev simulator |
| **Background Processing** | `OrderLifecycleBackgroundService` (`BackgroundService` + `PeriodicTimer` every 15 minutes) |
| **Notifications** | `INotificationService` (`WhatsAppGatewayNotificationService` with SQLite `NotificationLogs` fallback) |
| **Analytics & Charts** | Chart.js 4 (`wwwroot/js/admin-charts.js`) backed by `AdminAnalyticsService` |

---

## 3. Quick-Start Local Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.x`)

### Run Locally
```powershell
cd C:\Users\huani\.gemini\antigravity\scratch\KaryaJayaFootwear
dotnet restore
dotnet run
```
By default, `DbInitializer.InitializeAsync` runs automatically on startup, creates `pabrik_sepatu.db` in the content root, and seeds:
- **1 Administrator account** (`admin@pabrik.id` / `Pabrik123!`, Role: `Admin`). No `CustomerB2C` or `PartnerB2B` accounts are seeded; register them dynamically at `/Account/Register`.
- **14 footwear products** across `Sepatu`, `Sandal`, and `Boot` with size variants `38`–`44`.
- **3 wholesale tier rules** (`6–11` pairs = `15%`, `12–23` pairs = `25%`, `24+` pairs = `35%`).
- **3 B2B Starter Bundles** (*Paket Usaha Pemula*).

### `appsettings.json` Configuration Keys

| Section / Key | Default Value | Purpose |
| --- | --- | --- |
| `Database:Provider` | `"Sqlite"` | Switches EF Core between `"Sqlite"` and `"Postgres"` / `"PostgreSQL"` / `"Npgsql"` |
| `ConnectionStrings:DefaultConnection` | `"Data Source=pabrik_sepatu.db"` | SQLite database path (anchored to `ContentRootPath` if default) |
| `ConnectionStrings:PostgresConnection` | `"Host=localhost;Database=pabrik_sepatu;..."` | PostgreSQL connection string when `Database:Provider` is set to Postgres |
| `Midtrans:ServerKey` | `"SB-Mid-server-PLACEHOLDER..."` | Midtrans Snap Server Key (used for Basic Auth & webhook SHA-512 verification) |
| `Midtrans:ClientKey` | `"SB-Mid-client-PLACEHOLDER..."` | Midtrans `snap.js` Client Key |
| `Midtrans:IsProduction` | `false` | `false` = Sandbox (`app.sandbox.midtrans.com`), `true` = Production |
| `WhatsAppGateway:ApiUrl` | `""` | Optional WhatsApp HTTP gateway endpoint; when empty, logs locally to `NotificationLogs` |
| `WhatsAppGateway:ApiKey` | `""` | Bearer token for the WhatsApp HTTP gateway |
| `Warehouse:ApiKey` | `"PABRIK-GUDANG-1978"` | Shared secret for `X-Factory-Api-Key` header on `/api/v1/warehouse/*` |

---

## 4. Project Directory Map

```text
KaryaJayaFootwear/
├── Program.cs                          # DI registration, Cookie Auth/RBAC, HybridCache, Dual DB provider, endpoint mapping
├── appsettings.json                    # Database, Midtrans, WhatsAppGateway, and Warehouse configuration
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbSets, UTC DateTime conventions, unique indexes, decimal precision
│   └── DbInitializer.cs                # Schema creation, SQLite column compatibility, Admin-only + catalog/tier/bundle seeding
├── Models/
│   ├── Enums.cs                        # UserRole, FootwearCategory, PaymentMethod, ShippingMethod, OrderStatus
│   ├── Exceptions.cs                   # InsufficientStockException
│   ├── User.cs, Product.cs, ProductVariant.cs, WholesaleTierSetting.cs
│   ├── Order.cs, OrderItem.cs, NotificationLog.cs, StarterBundle.cs
│   └── CartModels.cs, ViewModels.cs    # Cart, filter, checkout, and Admin analytics DTOs
├── Options/
│   ├── MidtransOptions.cs
│   ├── WarehouseOptions.cs
│   └── WhatsAppGatewayOptions.cs
├── Services/
│   ├── AuthService.cs                  # Registration, PasswordHasher<User> login, cookie claims, B2B approval
│   ├── CatalogService.cs               # HybridCache-backed catalog/bundles & Admin product/variant CRUD
│   ├── PricingEngineService.cs         # HybridCache-backed lusin tiers & shipping cost calculation
│   ├── CartService.cs                  # Cookie cart, single/batch/bundle additions, live AvailableStock checks
│   ├── OrderFulfillmentService.cs      # Atomic order creation, stock reservation, lifecycle checks, warehouse manifest/dispatch
│   ├── OrderLifecycleBackgroundService.cs # 15-min PeriodicTimer worker for >24h expiration & B2B Tempo flagging
│   ├── IPaymentService.cs & MidtransPaymentService.cs # Snap API, SHA-512 webhook verification, stock settlement
│   ├── INotificationService.cs & WhatsAppGatewayNotificationService.cs # WhatsApp dispatch & SQLite NotificationLogs
│   ├── IFileStorageService.cs & LocalDiskFileStorageService.cs # Product image validation & wwwroot/uploads/products/
│   ├── AdminAnalyticsService.cs        # Real-time SQLite KPIs, trends, top products/sizes, low-stock & Tempo alerts
│   └── RetroSvgHelper.cs & FormatHelper.cs # Inline SVG footwear/stamp generators & Indonesian Rupiah/WIB formatting
├── Endpoints/
│   ├── ApiEndpoints.cs                 # POST /api/payments/webhook & /api/cart/* JSON endpoints
│   └── WarehouseEndpoints.cs           # /api/v1/warehouse/orders/{invoiceNumber} & /dispatch Minimal API
├── Pages/
│   ├── Index.cshtml(.cs)               # Vintage print-poster homepage, categories, wholesale tiers, starter bundles
│   ├── Products/                       # Index (filter + B2B simulation) & Details (variants, calculator, B2B matrix)
│   ├── Tools/FootRuler.cshtml(.cs)     # 1:1 printable millimeter foot ruler (38–44) with 50mm calibration box
│   ├── Cart/ & Checkout/               # Full cart ledger & checkout with role/logistics/payment guards
│   ├── Orders/Invoice.cshtml(.cs)      # 1980s carbon-copy invoice, Midtrans Snap, local callback sim, 90-day warranty stub
│   ├── Account/                        # Login, Register (B2C/B2B), Orders, Profile, Logout, AccessDenied
│   ├── Admin/                          # Index (analytics), Orders (+ WA logs & lifecycle check), Products, Partners, Discounts, WarehouseScan
│   └── Shared/                         # _Layout, _AdminLayout, _UserProfileMenu, _ProductVisualFrame, _StarterBundlesSection
└── wwwroot/
    ├── css/vintage.css                 # Print-era palette, halftone badges, double-ruled borders, @media print
    ├── js/storefront.js                # Laci Kasir drawer, rubber-stamp toasts, accessible profile dropdown
    ├── js/admin-charts.js              # Chart.js print-palette configuration
    └── uploads/products/               # Local product photo storage

Note: Containerization (`Dockerfile`, `deploy/`) and CI/CD (`.github/workflows/`) files are not yet included in this repository; see `docs/DEVELOPER_GUIDE.md` for deployment & persistent volume guidance.
```
