using System.Security.Cryptography;
using System.Text;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Options;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KaryaJayaFootwear.Endpoints;

public static class WarehouseEndpoints
{
    public sealed class DispatchRequestBody
    {
        public string CourierName { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
    }

    public static void MapWarehouseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/warehouse")
            .AddEndpointFilter(async (context, next) =>
            {
                var http = context.HttpContext;
                bool isAdmin = http.User.Identity?.IsAuthenticated == true &&
                               http.User.IsInRole(nameof(UserRole.Admin));

                if (!isAdmin)
                {
                    var warehouseOpts = http.RequestServices.GetRequiredService<IOptions<WarehouseOptions>>().Value;
                    var providedKey = http.Request.Headers["X-Factory-Api-Key"].ToString();

                    bool validKey = false;
                    if (!string.IsNullOrWhiteSpace(warehouseOpts.ApiKey) && !string.IsNullOrEmpty(providedKey))
                    {
                        byte[] expectedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(warehouseOpts.ApiKey));
                        byte[] actualDigest = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
                        validKey = CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest);
                    }

                    if (!validKey)
                    {
                        return Results.Json(
                            new { error = "Unauthorized", message = "Diperlukan header X-Factory-Api-Key yang valid atau sesi Administrator Pabrik." },
                            statusCode: StatusCodes.Status401Unauthorized);
                    }
                }

                return await next(context);
            });

        group.MapGet("/orders/{invoiceNumber}", async (
            string invoiceNumber,
            [FromServices] OrderFulfillmentService fulfillment,
            CancellationToken ct) =>
        {
            var manifest = await fulfillment.GetWarehouseManifestAsync(invoiceNumber, ct);
            if (manifest is null)
            {
                return Results.NotFound(new { error = "NotFound", message = $"Faktur '{invoiceNumber}' tidak ditemukan." });
            }

            return Results.Ok(manifest);
        });

        group.MapPost("/orders/{invoiceNumber}/dispatch", async (
            string invoiceNumber,
            [FromBody] DispatchRequestBody body,
            [FromServices] OrderFulfillmentService fulfillment,
            CancellationToken ct) =>
        {
            var result = await fulfillment.DispatchOrderFromWarehouseAsync(
                invoiceNumber,
                body.CourierName,
                body.TrackingNumber,
                ct);

            if (!result.Found)
            {
                return Results.NotFound(new { error = "NotFound", message = result.ErrorMessage });
            }

            if (!result.Success)
            {
                return Results.BadRequest(new { error = "InvalidOrderState", message = result.ErrorMessage });
            }

            return Results.Ok(new
            {
                message = $"Faktur {result.Manifest!.InvoiceNumber} berhasil diberangkatkan dari gudang.",
                order = result.Manifest
            });
        });
    }
}
