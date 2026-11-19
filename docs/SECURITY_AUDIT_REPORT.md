# Security Audit & Verification Report — Karya Jaya 1978

**Project:** `KaryaJayaFootwear` (.NET 10 ASP.NET Core Razor Pages + Minimal APIs)  
**Test Suite:** `KaryaJayaFootwear.SecurityTests` (xUnit 2.9.3 + `WebApplicationFactory<Program>`)  
**Verification Result:** `dotnet test` — **22 Passed, 0 Failed, 0 Skipped**

---

## 1. Executive Security Posture Summary

A white-box security audit and defensive code hardening pass was conducted across the `KaryaJayaFootwear` codebase covering business logic, RBAC, IDOR prevention, mass-assignment protection, webhook/warehouse API security, file upload validation, XSS encoding, CSRF protection, and HTTP security headers.

All identified security gaps were remediated in domain services, Minimal API endpoints, PageModels, and `Program.cs`, and verified through 22 automated HTTP integration test cases running against isolated temporary SQLite databases.

---

## 2. Audited Domains, Findings & Defensive Controls Applied

| Domain | Finding Identified | Remediated File(s) | Defensive Control Applied |
| --- | --- | --- | --- |
| **1. Pricing & Quantity Integrity** | `CartService.UpdateQuantityAsync` silently removed items when `quantity <= 0`; `AddBatchItemsAsync` ignored negative quantities instead of rejecting; `DetailsModel.OnPostAddToCartAsync` clamped `<= 0` silently. | `Services/CartService.cs`, `Endpoints/ApiEndpoints.cs`, `Pages/Products/Details.cshtml.cs`, `wwwroot/js/storefront.js` | Reject `qty <= 0` in `UpdateQuantityAsync` (`400 BadRequest` on `/api/cart/update`), reject negative values in `AddBatchItemsAsync`, reject `bundleId <= 0` in `AddStarterBundleAsync`, and enforce server-side `IsB2BApproved` & `Role == PartnerB2B` checks in `OrderFulfillmentService.PlaceOrderAsync`. |
| **2. Access Control & Invoice IDOR** | `OrderFulfillmentService.CanAccessInvoice` used non-constant-time `string.Equals` and allowed token fallback even when `order.UserId.HasValue`. | `Program.cs`, `Services/OrderFulfillmentService.cs` | Enforced `AdminOnly` on `/Admin/*`; restricted `order.UserId.HasValue` invoices strictly to `Admin` or owning `UserId`; enforced SHA-256 + `CryptographicOperations.FixedTimeEquals` on `OrderAccessToken` for guest invoices. |
| **3. Mass Assignment / Over-Posting** | `Pages/Admin/Discounts.cshtml.cs` bound directly to `List<WholesaleTierSetting>` EF Core entities. | `Pages/Admin/Discounts.cshtml.cs`, `Pages/Account/Register.cshtml.cs` | Replaced EF entity binding with dedicated `TierInputModel` DTO with `[Required]` and `[Range]` validation; `AuthService.RegisterAsync` strictly restricts roles to `CustomerB2C` or `PartnerB2B` with `IsB2BApproved = false`. |
| **4. Midtrans Webhook, Simulator & Warehouse API** | `ProcessWebhookNotificationAsync` did not validate `gross_amount` against `order.GrandTotal` and could re-process duplicate `TempoB2B` settlements; `OnPostSimulateCallbackAsync` lacked a server-side `IsDevelopment()` guard; `WarehouseEndpoints` allowed the dev key in non-Development. | `Services/MidtransPaymentService.cs`, `Pages/Orders/Invoice.cshtml.cs`, `Endpoints/WarehouseEndpoints.cs` | Added `gross_amount` equality check, strict settlement idempotency guard, `403 Forbidden` on `OnPostSimulateCallbackAsync` when `!_env.IsDevelopment()`, and `CryptographicOperations.FixedTimeEquals` + placeholder/dev-key rejection on `/api/v1/warehouse/*`. |
| **5. Concurrency & File Upload Hardening** | `LocalDiskFileStorageService` checked file extension and size (`<= 3 MB`) but did not inspect binary magic bytes. | `Services/LocalDiskFileStorageService.cs`, `Services/OrderFulfillmentService.cs` | Added binary magic-byte verification for JPEG (`FF D8 FF`), PNG (`89 50 4E 47 0D 0A 1A 0A`), and WebP (`RIFF....WEBP`), `Path.GetFileName` sanitization, and GUID filenames. Verified `ConcurrencyStamp` rotation on all stock mutations. |
| **6. XSS, CSRF & HTTP Headers** | Missing defensive HTTP security response headers and explicit cookie `SecurePolicy` in `Program.cs`. | `Program.cs`, `Pages/**/*.cshtml` | Verified all `@Html.Raw` calls only emit encoded SVG helpers or `JsonSerializer.Serialize`; added `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`, and `CookieSecurePolicy.SameAsRequest`. |

---

## 3. Automated Security Test Matrix (`KaryaJayaFootwear.SecurityTests`)

| Test Class | Test Case(s) | Verified Security Invariant |
| --- | --- | --- |
| `AccessControlTests` | `Anonymous_Requests_To_Admin_Routes_Redirect_To_Login` (7 routes)<br>`CustomerB2C_Requests_To_Admin_Routes_Are_Denied` (5 routes)<br>`Warehouse_Api_Returns_401_Without_Or_With_Invalid_Key_And_200_With_Valid_Key` | Anonymous and `CustomerB2C` access to `/Admin/*` is blocked; `/api/v1/warehouse/*` enforces `X-Factory-Api-Key` (`401` vs `200`). |
| `InvoiceOwnershipTests` | `Authenticated_UserA_Cannot_View_UserB_Invoice_IDOR_Prevented`<br>`Guest_Invoice_Requires_Exact_OrderAccessToken` | Cross-user invoice access is denied; guest invoice requires exact `OrderAccessToken`. |
| `RegistrationSecurityTests` | `OverPosting_Admin_Role_Or_IsB2BApproved_At_Registration_Is_Ignored` | Over-posting `Role=Admin` or `IsB2BApproved=true` cannot escalate privileges. |
| `CheckoutAndPricingSecurityTests` | `Unapproved_PartnerB2B_Cannot_Use_TempoB2B_Or_Claim_Wholesale_Discounts`<br>`Negative_And_Zero_Quantities_Are_Rejected_Without_Corrupting_Cart_Or_Stock` | Unapproved B2B accounts receive `0%` discount and cannot submit `TempoB2B`; `<= 0` quantities are rejected cleanly. |
| `PaymentWebhookSecurityTests` | `Webhook_Rejects_Invalid_Signature_And_Enforces_Idempotency_On_Duplicate_Settlement`<br>`Localhost_Callback_Simulator_Is_Forbidden_In_Production_Environment` | Invalid SHA-512 webhook signatures return `400`; duplicate settlement does not double-deduct `PhysicalStock`; simulator returns `403` in `Production`. |
| `InputAndUploadSecurityTests` | `Disguised_NonImage_Upload_Is_Rejected_By_Magic_Bytes_Validation`<br>`Xss_Payload_In_ShopName_Is_Html_Encoded_On_Admin_Partners_And_Invoices` | Disguised `.jpg` text/script payloads are rejected by magic-byte inspection; `<script>` payloads in `ShopName` are HTML-encoded. |

---

## 4. Pre-Deployment Production Security Checklist

1. **Secrets & Keys:** Replace placeholder/dev secrets in production environment variables or secret manager (`Midtrans:ServerKey`, `Midtrans:ClientKey`, `Midtrans:IsProduction=true`, `Warehouse:ApiKey` — must not be `"PABRIK-GUDANG-1978"`, and `WhatsAppGateway:ApiKey`).
2. **Admin Password:** Change the default seeded Admin password (`admin@pabrik.id` / `Pabrik123!`) immediately after first login in production.
3. **HTTPS & HSTS:** Serve behind a TLS-terminating reverse proxy (Nginx/Caddy/Azure App Service) with HTTPS enforced (`app.UseHsts()` is active when `!IsDevelopment()`).
4. **Database & Persistent Storage:** Mount `pabrik_sepatu.db` (or configure `Database:Provider="Postgres"` with `ConnectionStrings:PostgresConnection`), `wwwroot/uploads/products/`, and `.dp-keys/` on persistent, backed-up storage.
5. **Environment Variable:** Ensure `ASPNETCORE_ENVIRONMENT=Production` so the local Midtrans callback simulator is disabled (`403 Forbidden`) and detailed developer exception pages are off.
