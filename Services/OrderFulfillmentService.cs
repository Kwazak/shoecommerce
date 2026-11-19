using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace KaryaJayaFootwear.Services;

public sealed class PlaceOrderResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Order? Order { get; init; }
    public SnapTransactionResult? SnapResult { get; init; }
}

public sealed class OrderLifecycleCheckResult
{
    public int ExpiredOrdersCount { get; init; }
    public int ReleasedPairsCount { get; init; }
    public int FlaggedTempoNearDueCount { get; init; }
}

public sealed class WarehouseManifestDto
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? ShopName { get; init; }
    public string ShippingCity { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public string ShippingMethod { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CourierName { get; init; }
    public string? TrackingNumber { get; init; }
    public int TotalPairs { get; init; }
    public List<WarehouseManifestItemDto> Items { get; init; } = new();
}

public sealed class WarehouseManifestItemDto
{
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string ColorName { get; init; } = string.Empty;
    public int Size { get; init; }
    public int Quantity { get; init; }
}

public sealed class WarehouseDispatchResult
{
    public bool Found { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public WarehouseManifestDto? Manifest { get; init; }
}

public sealed class OrderFulfillmentService
{
    private readonly AppDbContext _db;
    private readonly CartService _cartService;
    private readonly PricingEngineService _pricing;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notifications;
    private readonly HybridCache _cache;

    public OrderFulfillmentService(
        AppDbContext db,
        CartService cartService,
        PricingEngineService pricing,
        IPaymentService paymentService,
        INotificationService notifications,
        HybridCache cache)
    {
        _db = db;
        _cartService = cartService;
        _pricing = pricing;
        _paymentService = paymentService;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(CheckoutInputModel input, ClaimsPrincipal principal)
    {
        User? currentUser = null;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idClaim, out var uid))
            {
                currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            }
        }

        // Guard: Admin accounts must not submit customer orders
        if (currentUser?.Role == UserRole.Admin)
        {
            return new PlaceOrderResult
            {
                Success = false,
                ErrorMessage = "Akun Administrator Pabrik tidak dapat membuat pesanan pelanggan. Gunakan akun Pembeli Eceran atau Mitra B2B."
            };
        }

        bool isApprovedB2B = currentUser is { Role: UserRole.PartnerB2B, IsB2BApproved: true };

        // Guard: Tempo Mitra B2B (14 Hari) is strictly for approved PartnerB2B
        if (input.PaymentMethod == PaymentMethod.TempoB2B && !isApprovedB2B)
        {
            return new PlaceOrderResult
            {
                Success = false,
                ErrorMessage = "Fasilitas Tempo Mitra B2B (14 Hari) hanya tersedia bagi Mitra Toko B2B yang telah disetujui Admin Pabrik."
            };
        }

        // Guard: Ekspedisi Kargo Truk only for B2B or >= 6 pairs
        var cart = await _cartService.GetCartSummaryAsync(principal, simulateB2B: false);
        if (cart.Items.Count == 0)
        {
            return new PlaceOrderResult
            {
                Success = false,
                ErrorMessage = "Laci Kasir masih kosong. Silakan pilih alas kaki terlebih dahulu."
            };
        }

        if (input.ShippingMethod == ShippingMethod.EkspedisiKargoTruk && !isApprovedB2B && cart.TotalPairs < 6)
        {
            return new PlaceOrderResult
            {
                Success = false,
                ErrorMessage = "Ekspedisi Kargo Truk khusus untuk pesanan partai lusinan (minimal 6 pasang) atau Mitra B2B."
            };
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var variantIds = cart.Items.Select(i => i.VariantId).ToList();
            var dbVariants = await _db.ProductVariants
                .Include(v => v.Product)
                .Where(v => variantIds.Contains(v.Id) && v.Product.IsActive)
                .ToDictionaryAsync(v => v.Id);

            foreach (var item in cart.Items)
            {
                if (!dbVariants.TryGetValue(item.VariantId, out var variant))
                {
                    await tx.RollbackAsync();
                    throw new InsufficientStockException($"Produk {item.ProductName} sudah tidak tersedia.");
                }

                if (variant.AvailableStock < item.Quantity)
                {
                    await tx.RollbackAsync();
                    throw new InsufficientStockException(
                        $"Stok tersedia {variant.Product.Name} ukuran {variant.Size} ({variant.ColorName}) hanya tersisa {Math.Max(0, variant.AvailableStock)} pasang.");
                }
            }

            // Recompute authoritative pricing inside service
            var tiers = await _pricing.GetActiveTiersAsync();
            int totalPairs = cart.Items.Sum(i => i.Quantity);
            var activeTier = isApprovedB2B ? _pricing.ResolveTierForPairs(tiers, totalPairs) : null;
            decimal discountPct = activeTier?.DiscountPercent ?? 0m;
            bool isWholesaleOrder = isApprovedB2B && discountPct > 0m;

            var initialStatus = input.PaymentMethod switch
            {
                PaymentMethod.COD => OrderStatus.DijahitDikemas,
                PaymentMethod.TempoB2B => OrderStatus.DijahitDikemas,
                _ => OrderStatus.MenungguPembayaran
            };

            decimal grossSubtotal = 0m;
            decimal netSubtotal = 0m;
            var orderItems = new List<OrderItem>();

            foreach (var item in cart.Items)
            {
                var variant = dbVariants[item.VariantId];

                if (initialStatus == OrderStatus.MenungguPembayaran)
                {
                    variant.ReservedStock += item.Quantity;
                }
                else
                {
                    // COD & Approved B2B Tempo 14 Hari deduct PhysicalStock immediately
                    variant.PhysicalStock -= item.Quantity;
                }
                variant.ConcurrencyStamp = Guid.NewGuid();

                decimal retailUnit = variant.Product.RetailPrice;
                decimal appliedUnit = discountPct > 0m
                    ? _pricing.CalculateDiscountedUnitPrice(retailUnit, discountPct)
                    : retailUnit;

                decimal lineRetail = retailUnit * item.Quantity;
                decimal lineTotal = appliedUnit * item.Quantity;
                grossSubtotal += lineRetail;
                netSubtotal += lineTotal;

                orderItems.Add(new OrderItem
                {
                    ProductId = variant.ProductId,
                    ProductVariantId = variant.Id,
                    ProductName = variant.Product.Name,
                    ProductSku = variant.Product.Sku,
                    Category = variant.Product.Category,
                    Size = variant.Size,
                    ColorName = variant.ColorName,
                    SvgIllustrationKey = variant.Product.SvgIllustrationKey,
                    ColorHex = variant.ColorHex,
                    Quantity = item.Quantity,
                    UnitRetailPrice = retailUnit,
                    UnitAppliedPrice = appliedUnit,
                    LineTotal = lineTotal
                });
            }

            decimal discountAmount = Math.Max(0m, grossSubtotal - netSubtotal);
            decimal shippingCost = _pricing.CalculateShippingCost(input.ShippingMethod, totalPairs, isWholesaleOrder || isApprovedB2B);
            decimal grandTotal = netSubtotal + shippingCost;

            string invoiceNo = await GenerateUniqueInvoiceNumberAsync();
            string accessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

            var order = new Order
            {
                InvoiceNumber = invoiceNo,
                OrderAccessToken = accessToken,
                UserId = currentUser?.Id,
                CustomerName = input.CustomerName.Trim(),
                CustomerEmail = input.CustomerEmail.Trim().ToLowerInvariant(),
                CustomerPhone = input.CustomerPhone.Trim(),
                ShopName = isApprovedB2B ? (currentUser?.ShopName ?? input.ShopName?.Trim()) : input.ShopName?.Trim(),
                ShippingCity = input.ShippingCity.Trim(),
                ShippingAddress = input.ShippingAddress.Trim(),
                ShippingMethod = input.ShippingMethod,
                PaymentMethod = input.PaymentMethod,
                OrderStatus = initialStatus,
                IsWholesaleOrder = isApprovedB2B,
                TotalPairs = totalPairs,
                GrossSubtotal = grossSubtotal,
                DiscountPercentApplied = discountPct,
                DiscountAmount = discountAmount,
                ShippingCost = shippingCost,
                GrandTotal = grandTotal,
                TempoDueDate = input.PaymentMethod == PaymentMethod.TempoB2B ? DateTime.UtcNow.AddDays(14) : null,
                IsTempoNearDue = false,
                CreatedAt = DateTime.UtcNow,
                Notes = input.Notes?.Trim(),
                Items = orderItems
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _cache.RemoveByTagAsync("catalog");
            _cartService.ClearCart();

            await _notifications.SendOrderCreatedAsync(order);

            SnapTransactionResult? snapResult = null;
            if (order.PaymentMethod == PaymentMethod.MidtransOnline)
            {
                snapResult = await _paymentService.CreateSnapTransactionAsync(order);
                if (snapResult.Success && !string.IsNullOrWhiteSpace(snapResult.SnapToken))
                {
                    order.MidtransSnapToken = snapResult.SnapToken;
                    order.MidtransRedirectUrl = snapResult.RedirectUrl;
                    await _db.SaveChangesAsync();
                }
            }

            return new PlaceOrderResult
            {
                Success = true,
                Order = order,
                SnapResult = snapResult
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync();
            throw new InsufficientStockException(
                "Stok gudang baru saja berubah karena pesanan lain yang bersamaan. Silakan periksa kembali Laci Kasir Anda.",
                ex);
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Order?> GetOrderForInvoiceAsync(int orderId, ClaimsPrincipal principal, string? accessToken)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null) return null;

        if (!CanAccessInvoice(order, principal, accessToken))
        {
            return null;
        }

        return order;
    }

    public bool CanAccessInvoice(Order order, ClaimsPrincipal principal, string? accessToken)
    {
        if (principal.IsInRole(nameof(UserRole.Admin)))
        {
            return true;
        }

        if (order.UserId.HasValue)
        {
            if (principal.Identity?.IsAuthenticated == true)
            {
                var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(idClaim, out var uid) && order.UserId.Value == uid)
                {
                    return true;
                }
            }
            return false;
        }

        // B2C Guest checkout (UserId is null): validate via unguessable OrderAccessToken in constant time
        if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(order.OrderAccessToken))
        {
            byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(order.OrderAccessToken));
            byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken.Trim()));
            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }

        return false;
    }

    public async Task<SnapTransactionResult> EnsureSnapTokenForOrderAsync(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.MidtransSnapToken))
        {
            return new SnapTransactionResult
            {
                Success = true,
                SnapToken = order.MidtransSnapToken,
                RedirectUrl = order.MidtransRedirectUrl
            };
        }

        var res = await _paymentService.CreateSnapTransactionAsync(order);
        if (res.Success && !string.IsNullOrWhiteSpace(res.SnapToken))
        {
            order.MidtransSnapToken = res.SnapToken;
            order.MidtransRedirectUrl = res.RedirectUrl;
            await _db.SaveChangesAsync();
        }
        return res;
    }

    public async Task<(bool Success, string Message)> SimulateLocalMidtransSettlementAsync(Order order)
    {
        var payload = new MidtransNotificationPayload
        {
            order_id = order.InvoiceNumber,
            status_code = "200",
            gross_amount = ((long)order.GrandTotal).ToString() + ".00",
            transaction_status = "settlement",
            fraud_status = "accept",
            payment_type = "qris_simulasi_lokal",
            transaction_id = $"LOKAL-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        return await _paymentService.ProcessWebhookNotificationAsync(payload, bypassSignatureForLocalSim: true);
    }

    public async Task<List<Order>> GetOrdersForUserAsync(int userId)
    {
        return await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetOrdersForAdminAsync(string? search, OrderStatus? statusFilter)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                o.InvoiceNumber.ToLower().Contains(term) ||
                o.CustomerName.ToLower().Contains(term) ||
                (o.ShopName != null && o.ShopName.ToLower().Contains(term)) ||
                o.ShippingCity.ToLower().Contains(term));
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(o => o.OrderStatus == statusFilter.Value);
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> UpdateOrderStatusByAdminAsync(int orderId, OrderStatus newStatus)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            return (false, "Faktur tidak ditemukan.");
        }

        var oldStatus = order.OrderStatus;
        if (oldStatus == newStatus)
        {
            return (true, "Status faktur tidak berubah.");
        }

        static bool IsCancelledOrExpired(OrderStatus s) => s is OrderStatus.Dibatalkan or OrderStatus.Kedaluwarsa;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var variantIds = order.Items.Select(i => i.ProductVariantId).Distinct().ToList();
            var variants = await _db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            // 1. Moving to Cancelled/Expired from an active status
            if (IsCancelledOrExpired(newStatus) && !IsCancelledOrExpired(oldStatus))
            {
                foreach (var item in order.Items)
                {
                    if (!variants.TryGetValue(item.ProductVariantId, out var variant)) continue;

                    if (oldStatus == OrderStatus.MenungguPembayaran)
                    {
                        variant.ReservedStock = Math.Max(0, variant.ReservedStock - item.Quantity);
                    }
                    else
                    {
                        variant.PhysicalStock += item.Quantity;
                    }
                    variant.ConcurrencyStamp = Guid.NewGuid();
                }
                order.IsTempoNearDue = false;
            }
            // 2. Moving from MenungguPembayaran into confirmed fulfillment statuses -> convert Reserved into Physical deduction
            else if (oldStatus == OrderStatus.MenungguPembayaran && !IsCancelledOrExpired(newStatus))
            {
                foreach (var item in order.Items)
                {
                    if (!variants.TryGetValue(item.ProductVariantId, out var variant)) continue;

                    variant.PhysicalStock = Math.Max(0, variant.PhysicalStock - item.Quantity);
                    variant.ReservedStock = Math.Max(0, variant.ReservedStock - item.Quantity);
                    variant.ConcurrencyStamp = Guid.NewGuid();
                }
            }
            // 3. Reopening from Cancelled/Expired to an active status
            else if (IsCancelledOrExpired(oldStatus) && !IsCancelledOrExpired(newStatus))
            {
                foreach (var item in order.Items)
                {
                    if (!variants.TryGetValue(item.ProductVariantId, out var variant) || variant.AvailableStock < item.Quantity)
                    {
                        await tx.RollbackAsync();
                        return (false, $"Stok {item.ProductName} Uk.{item.Size} tidak cukup untuk mengaktifkan kembali faktur ini.");
                    }

                    if (newStatus == OrderStatus.MenungguPembayaran)
                    {
                        variant.ReservedStock += item.Quantity;
                    }
                    else
                    {
                        variant.PhysicalStock -= item.Quantity;
                    }
                    variant.ConcurrencyStamp = Guid.NewGuid();
                }
            }

            order.OrderStatus = newStatus;

            if ((newStatus == OrderStatus.DibayarSiapDikemas || newStatus == OrderStatus.Selesai) && !order.PaidAt.HasValue)
            {
                order.PaidAt = DateTime.UtcNow;
                order.IsTempoNearDue = false;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            await _cache.RemoveByTagAsync("catalog");

            if (newStatus == OrderStatus.DibayarSiapDikemas)
            {
                await _notifications.SendPaymentConfirmedAsync(order);
            }
            else if (newStatus == OrderStatus.Dikirim)
            {
                await _notifications.SendOrderDispatchedAsync(order);
            }

            return (true, $"Status faktur {order.InvoiceNumber} diperbarui menjadi '{newStatus.ToIndonesianLabel()}'.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return (false, "Bentrok pembaruan stok saat mengubah status faktur. Silakan muat ulang dan coba lagi.");
        }
    }

    public async Task<bool> MarkTempoOrderPaidAsync(int orderId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return false;

        order.PaidAt ??= DateTime.UtcNow;
        order.IsTempoNearDue = false;
        if (order.OrderStatus == OrderStatus.MenungguPembayaran)
        {
            order.OrderStatus = OrderStatus.DibayarSiapDikemas;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<OrderLifecycleCheckResult> RunOrderLifecycleCheckAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var expirationCutoff = nowUtc.AddHours(-24);
        var tempoFlagCutoff = nowUtc.AddDays(2);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Auto-expire unpaid MenungguPembayaran orders older than 24 hours & release ReservedStock
            var expiredOrders = await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.OrderStatus == OrderStatus.MenungguPembayaran && o.CreatedAt <= expirationCutoff)
                .ToListAsync(ct);

            int releasedPairs = 0;
            if (expiredOrders.Count > 0)
            {
                var variantIds = expiredOrders
                    .SelectMany(o => o.Items.Select(i => i.ProductVariantId))
                    .Distinct()
                    .ToList();

                var variants = await _db.ProductVariants
                    .Where(v => variantIds.Contains(v.Id))
                    .ToDictionaryAsync(v => v.Id, ct);

                foreach (var order in expiredOrders)
                {
                    order.OrderStatus = OrderStatus.Kedaluwarsa;
                    foreach (var item in order.Items)
                    {
                        if (variants.TryGetValue(item.ProductVariantId, out var variant))
                        {
                            variant.ReservedStock = Math.Max(0, variant.ReservedStock - item.Quantity);
                            variant.ConcurrencyStamp = Guid.NewGuid();
                            releasedPairs += item.Quantity;
                        }
                    }
                }
            }

            // 2. Flag active unpaid Tempo B2B orders within 2 days of their 14-day due date
            var unpaidTempoOrders = await _db.Orders
                .Where(o => o.PaymentMethod == PaymentMethod.TempoB2B &&
                            o.PaidAt == null &&
                            o.OrderStatus != OrderStatus.Dibatalkan &&
                            o.OrderStatus != OrderStatus.Kedaluwarsa &&
                            o.TempoDueDate != null)
                .ToListAsync(ct);

            int flaggedCount = 0;
            foreach (var order in unpaidTempoOrders)
            {
                bool nearDue = order.TempoDueDate!.Value <= tempoFlagCutoff;
                order.IsTempoNearDue = nearDue;
                if (nearDue) flaggedCount++;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (expiredOrders.Count > 0)
            {
                await _cache.RemoveByTagAsync("catalog", ct);
            }

            return new OrderLifecycleCheckResult
            {
                ExpiredOrdersCount = expiredOrders.Count,
                ReleasedPairsCount = releasedPairs,
                FlaggedTempoNearDueCount = flaggedCount
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WarehouseManifestDto?> GetWarehouseManifestAsync(string invoiceNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return null;
        var normalized = invoiceNumber.Trim().ToLowerInvariant();

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.InvoiceNumber.ToLower() == normalized, ct);

        return order is null ? null : MapManifest(order);
    }

    public async Task<WarehouseDispatchResult> DispatchOrderFromWarehouseAsync(
        string invoiceNumber,
        string courierName,
        string trackingNumber,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return new WarehouseDispatchResult { Found = false, ErrorMessage = "Nomor faktur wajib diisi." };
        }

        if (string.IsNullOrWhiteSpace(courierName) || string.IsNullOrWhiteSpace(trackingNumber))
        {
            return new WarehouseDispatchResult
            {
                Found = true,
                Success = false,
                ErrorMessage = "Nama ekspedisi/kurir (courierName) dan nomor resi (trackingNumber) wajib diisi."
            };
        }

        var normalized = invoiceNumber.Trim().ToLowerInvariant();
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.InvoiceNumber.ToLower() == normalized, ct);

        if (order is null)
        {
            return new WarehouseDispatchResult
            {
                Found = false,
                Success = false,
                ErrorMessage = $"Faktur '{invoiceNumber}' tidak ditemukan."
            };
        }

        bool isReadyToDispatch =
            order.OrderStatus is OrderStatus.DibayarSiapDikemas or OrderStatus.DijahitDikemas ||
            (order.PaymentMethod == PaymentMethod.TempoB2B &&
             order.OrderStatus != OrderStatus.Dibatalkan &&
             order.OrderStatus != OrderStatus.Kedaluwarsa &&
             order.OrderStatus != OrderStatus.Dikirim &&
             order.OrderStatus != OrderStatus.Selesai);

        if (!isReadyToDispatch)
        {
            return new WarehouseDispatchResult
            {
                Found = true,
                Success = false,
                ErrorMessage = $"Faktur {order.InvoiceNumber} berstatus '{order.OrderStatus.ToIndonesianLabel()}' sehingga belum/tidak dapat diberangkatkan dari gudang."
            };
        }

        order.CourierName = courierName.Trim();
        order.TrackingNumber = trackingNumber.Trim();
        order.DispatchedAt = DateTime.UtcNow;
        order.OrderStatus = OrderStatus.Dikirim;

        await _db.SaveChangesAsync(ct);
        await _notifications.SendOrderDispatchedAsync(order, ct);

        return new WarehouseDispatchResult
        {
            Found = true,
            Success = true,
            Manifest = MapManifest(order)
        };
    }

    private static WarehouseManifestDto MapManifest(Order order) => new()
    {
        InvoiceNumber = order.InvoiceNumber,
        CustomerName = order.CustomerName,
        ShopName = order.ShopName,
        ShippingCity = order.ShippingCity,
        ShippingAddress = order.ShippingAddress,
        ShippingMethod = order.ShippingMethod.ToIndonesianLabel(),
        PaymentMethod = order.PaymentMethod.ToIndonesianLabel(),
        Status = order.OrderStatus.ToIndonesianLabel(),
        CourierName = order.CourierName,
        TrackingNumber = order.TrackingNumber,
        TotalPairs = order.TotalPairs,
        Items = order.Items.Select(i => new WarehouseManifestItemDto
        {
            ProductSku = i.ProductSku,
            ProductName = i.ProductName,
            ColorName = i.ColorName,
            Size = i.Size,
            Quantity = i.Quantity
        }).ToList()
    };

    private async Task<string> GenerateUniqueInvoiceNumberAsync()
    {
        var datePart = DateTime.UtcNow.AddHours(7).ToString("yyyyMMdd");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var suffix = RandomNumberGenerator.GetInt32(1000, 9999);
            var candidate = $"FKJ-{datePart}-{suffix}";
            if (!await _db.Orders.AnyAsync(o => o.InvoiceNumber == candidate))
            {
                return candidate;
            }
        }
        return $"FKJ-{datePart}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
