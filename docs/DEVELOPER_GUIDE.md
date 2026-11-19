# Developer Guide — Karya Jaya 1978

This guide documents the architectural rules, core domain workflows, external integrations, and operational guidelines for maintaining and extending **KaryaJayaFootwear**.

---

## 1. Architectural Guardrails

1. **Thin PageModels (`.cshtml.cs`):**
   - PageModels must only bind HTTP inputs, invoke domain services (`CatalogService`, `CartService`, `PricingEngineService`, `OrderFulfillmentService`, `AuthService`, `AdminAnalyticsService`), set `TempData` toast messages, and return `IActionResult`.
   - Never place raw EF Core `AppDbContext` queries, database transactions, or wholesale discount math inside `.cshtml.cs` files.
2. **Focused Domain Services (No Generic Repository Bloat):**
   - `AppDbContext` is injected directly into purposeful domain services. Do not wrap `DbSet<T>` in generic `IRepository<T>` or `UnitOfWork` abstractions.
3. **Custom Cookie Authentication & RBAC (No Identity Scaffolding):**
   - Authentication uses a custom `User` entity (`Models/User.cs`) with `PasswordHasher<User>` and Cookie Authentication (`KaryaJayaAuth`).
   - Authorization is enforced in `Program.cs` via Razor Pages conventions:
     - `/Admin` folder requires `AdminOnly` (`UserRole.Admin`).
     - `/Account/Orders` and `/Account/Profile` require authentication (`[Authorize]`).
   - **Live B2B Verification Check:** `CartService.IsUserApprovedB2BAsync` and `OrderFulfillmentService.PlaceOrderAsync` query `Users.IsB2BApproved` directly from the database rather than trusting potentially stale cookie claims.
   - **Invoice Ownership Guard:** `OrderFulfillmentService.CanAccessInvoice` permits access only to `Admin`, the owning `UserId`, or guests presenting the unguessable 48-character hex `OrderAccessToken` query parameter.
4. **UI & Asset Guardrails:**
   - **Zero Bootstrap / Zero jQuery:** All layout and styling use Tailwind CDN + `wwwroot/css/vintage.css` + Vanilla JS (`wwwroot/js/storefront.js`).
   - **Zero Native Browser Dialogs:** Never use `alert()`, `confirm()`, or `prompt()`. Use `window.showStampToast(message, type)` for feedback and inline two-step confirmation controls for destructive actions (see `Pages/Admin/Products.cshtml`).
   - **Zero Broken Images:** Always render product visuals through `Pages/Shared/_ProductVisualFrame.cshtml`, which falls back to `RetroSvgHelper.RenderFootwearSvg` when `ImageUrl` is empty or fails to load (`onerror`).

---

## 2. Core Domain Workflows

### A. Pricing Engine (`Services/PricingEngineService.cs`)
- Active discount tiers are stored in `WholesaleTierSettings` and cached in `HybridCache` under key `"wholesale-tiers:active"` (tag `"wholesale-tiers"`).
- Default seeded tiers:
  - `6–11` pairs (`½ Kodi`): `15% OFF`
  - `12–23` pairs (`1 Kodi`): `25% OFF`
  - `24+` pairs (`≥ 2 Kodi`): `35% OFF`
- Unit prices are rounded to the nearest `Rp 100` (`Math.Round(raw / 100m, MidpointRounding.AwayFromZero) * 100m`).
- **Role Enforcement:** Only `PartnerB2B` accounts with `IsB2BApproved == true` receive wholesale discounts and `TempoB2B` (14-day credit) at checkout. Unauthenticated users and `CustomerB2C` users can preview wholesale pricing via `?SimulateB2B=true` on `/Products` and `/Products/Details/{id}`, but authoritative checkout totals are always recomputed server-side inside `OrderFulfillmentService.PlaceOrderAsync`.

### B. Inventory Reservation, Concurrency & Lifecycle (`OrderFulfillmentService` & `OrderLifecycleBackgroundService`)

```text
  [ProductVariant]
  PhysicalStock  (actual pairs in warehouse)
  ReservedStock  (held by active MenungguPembayaran orders)
  AvailableStock = PhysicalStock - ReservedStock
  ConcurrencyStamp (Guid, [ConcurrencyCheck])

  Order Created:
    ├── MidtransOnline (MenungguPembayaran) ──▶ ReservedStock += qty
    │     ├── Payment Confirmed (Webhook / Local Sim) ──▶ PhysicalStock -= qty, ReservedStock -= qty
    │     └── Cancelled / Expired (>24h)              ──▶ ReservedStock -= qty (released)
    └── COD / Approved TempoB2B (DijahitDikemas) ───────▶ PhysicalStock -= qty immediately
```

- **Optimistic Concurrency:** Every stock mutation regenerates `variant.ConcurrencyStamp = Guid.NewGuid()`. Inside `OrderFulfillmentService.PlaceOrderAsync`, an explicit transaction verifies `variant.AvailableStock >= item.Quantity`; if stock is insufficient or a `DbUpdateConcurrencyException` occurs, the transaction rolls back and throws `InsufficientStockException`, which `Pages/Checkout/Index.cshtml.cs` catches and surfaces via `TempData["ToastError"]`.
- **Background Worker (`Services/OrderLifecycleBackgroundService.cs`):**
  - Runs as a hosted `BackgroundService` using `PeriodicTimer(TimeSpan.FromMinutes(15))`.
  - Injects `IServiceScopeFactory` and creates an `AsyncServiceScope` per tick to resolve scoped `OrderFulfillmentService`.
  - Calls `RunOrderLifecycleCheckAsync`:
    1. Transitions `MenungguPembayaran` orders older than 24 hours to `OrderStatus.Kedaluwarsa` and releases `ReservedStock`.
    2. Sets `Order.IsTempoNearDue = true` for unpaid `TempoB2B` orders within 2 days of `TempoDueDate`.
  - Admins can trigger this exact method immediately from `/Admin/Orders` via **"Jalankan Pengecekan Jatuh Tempo Sekarang"**.

### C. Caching (`HybridCache`) & Notifications (`INotificationService`)
- **`HybridCache` Tags:**
  - `"catalog"` caches active product snapshots (`"catalog:active-products"`) and starter bundles (`"catalog:starter-bundles"`) in `CatalogService`. Invalidated via `RemoveByTagAsync("catalog")` on Admin product/stock/colorway edits and on order placement, payment settlement, cancellation, or expiration.
  - `"wholesale-tiers"` caches active tiers in `PricingEngineService` and is invalidated on `/Admin/Discounts` updates.
- **WhatsApp Notification Abstraction (`WhatsAppGatewayNotificationService`):**
  - Triggered on order creation (`PesananDibuat`), payment confirmation (`NotaLunas`), and shipment dispatch (`ResiDikirim`).
  - When `WhatsAppGateway:ApiUrl` and `WhatsAppGateway:ApiKey` are empty, records `DeliveryStatus = "Tersimpan (Mode Simulasi Lokal)"` in SQLite `NotificationLogs`, viewable in the **"Log Pesan WhatsApp"** drawer on `/Admin/Orders`.

---

## 3. External Integrations & APIs

### Midtrans Snap & Webhook (`MidtransPaymentService` & `Endpoints/ApiEndpoints.cs`)
- **Snap Token Creation:** Calls `{SnapApiBaseUrl}/snap/v1/transactions` via `IHttpClientFactory` (`"Midtrans"`) using Basic Auth (`Base64(ServerKey + ":")`). If `MidtransOptions.HasValidKeys` is `false`, returns a graceful placeholder notice so `/Orders/Invoice/{id}` remains functional.
- **Webhook (`POST /api/payments/webhook`):** Validates `signature_key` using `SHA512(order_id + status_code + gross_amount + ServerKey)` with `CryptographicOperations.FixedTimeEquals`. On `settlement` (or `capture` + `accept`), converts `ReservedStock` into `PhysicalStock` deduction and transitions the order to `DibayarSiapDikemas`.
- **Local Development Simulator:** In `Development` environment, `/Orders/Invoice/{id}` displays **"Simulasi Callback Lunas Midtrans (Mode Uji Lokal)"**, invoking `OrderFulfillmentService.SimulateLocalMidtransSettlementAsync` to run the same settlement transition without ngrok.

### Warehouse Barcode Scanner Minimal API (`Endpoints/WarehouseEndpoints.cs`)
- **Security Filter:** Requires either an authenticated `Admin` cookie session OR an `X-Factory-Api-Key` header matching `Warehouse:ApiKey` (`appsettings.json`), verified via SHA-256 + `CryptographicOperations.FixedTimeEquals`. Unauthorized requests receive `401 Unauthorized`.
- **`GET /api/v1/warehouse/orders/{invoiceNumber}`:** Case-insensitive invoice lookup returning manifest JSON (`InvoiceNumber`, `CustomerName`, `ShopName`, `ShippingMethod`, `Status`, `CourierName`, `TrackingNumber`, `TotalPairs`, `Items`) or `404 NotFound`.
- **`POST /api/v1/warehouse/orders/{invoiceNumber}/dispatch`:** Accepts `{ "courierName": "...", "trackingNumber": "..." }`, validates the order is ready for dispatch (`DibayarSiapDikemas`, `DijahitDikemas`, or active `TempoB2B`), sets `OrderStatus.Dikirim` + `DispatchedAt`, saves tracking fields, and triggers `INotificationService.SendOrderDispatchedAsync`.
- **UI Client:** `/Admin/WarehouseScan` provides a mobile-friendly, barcode-wedge-ready packing checklist and dispatch station calling these endpoints via `fetch()`.

---

## 4. Database Provider & Deployment Operations

### Dual Database Provider (`Program.cs` & `Data/AppDbContext.cs`)
- Switch between SQLite and PostgreSQL using `Database:Provider` in `appsettings.json` (`"Sqlite"` vs `"Postgres"` / `"PostgreSQL"` / `"Npgsql"`).
- `AppDbContext.ConfigureConventions` applies `UtcDateTimeConverter` and `NullableUtcDateTimeConverter` so all `DateTime` values carry `DateTimeKind.Utc` for PostgreSQL `timestamp with time zone` compatibility.
- SQLite-specific `PRAGMA` / `ALTER TABLE` compatibility checks in `DbInitializer` are guarded by `if (context.Database.IsSqlite())`.

### Persistent Storage Paths (For Future Docker / VPS / Self-Hosted Deployments)
If packaging the application into containers or deploying to a Linux VPS / Windows host, ensure the following paths are mounted to persistent storage:
- **SQLite Database:** `pabrik_sepatu.db` (plus `-wal`/`-shm` sidecars) in `ContentRootPath` (or switch `Database:Provider` to `"Postgres"`).
- **Uploaded Product Photos:** `wwwroot/uploads/products/` (written by `LocalDiskFileStorageService`).
- **Data Protection Keys:** `.dp-keys/` in `ContentRootPath` (configured in `Program.cs` via `PersistKeysToFileSystem` to preserve auth cookies and antiforgery tokens across restarts).

### Future ERP Integration Extension Points
- **Order & Inventory Sync:** Hook into `OrderFulfillmentService.PlaceOrderAsync` and `DispatchOrderFromWarehouseAsync` (or add an `IOutboxPublisher` alongside `INotificationService`) to push confirmed sales orders and stock movements to an external ERP.
- **Product Master & Costing:** Extend `CatalogService` and `ProductVariant` (`PhysicalStock` / `ReservedStock`) to synchronize SKU master data and warehouse stock counts from ERP production batches.
