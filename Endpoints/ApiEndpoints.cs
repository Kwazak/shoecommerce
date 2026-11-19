using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;

namespace KaryaJayaFootwear.Endpoints;

public static class ApiEndpoints
{
    public sealed class AddCartBody
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public sealed class UpdateCartBody
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class AddBundleBody
    {
        public int BundleId { get; set; }
    }

    public static void MapApplicationApiEndpoints(this WebApplication app)
    {
        // Midtrans Snap HTTP Notification Webhook
        app.MapPost("/api/payments/webhook", async (
            [FromBody] MidtransNotificationPayload payload,
            [FromServices] IPaymentService paymentService) =>
        {
            var (success, message) = await paymentService.ProcessWebhookNotificationAsync(payload, bypassSignatureForLocalSim: false);
            if (!success)
            {
                return Results.BadRequest(new { status = "error", message });
            }

            return Results.Ok(new { status = "ok", message });
        });

        // Laci Kasir (Slide-out Cart Drawer) JSON API
        app.MapGet("/api/cart", async (HttpContext ctx, [FromServices] CartService cartService, bool? simulateB2B) =>
        {
            var summary = await cartService.GetCartSummaryAsync(ctx.User, simulateB2B ?? false);
            return Results.Ok(summary);
        });

        app.MapPost("/api/cart/add", async ([FromBody] AddCartBody body, [FromServices] CartService cartService) =>
        {
            var res = await cartService.AddToCartAsync(body.VariantId, body.Quantity);
            return res.Success ? Results.Ok(res) : Results.BadRequest(res);
        });

        app.MapPost("/api/cart/update", async ([FromBody] UpdateCartBody body, [FromServices] CartService cartService) =>
        {
            var res = await cartService.UpdateQuantityAsync(body.VariantId, body.Quantity);
            return Results.Ok(res);
        });

        app.MapPost("/api/cart/remove", async ([FromBody] UpdateCartBody body, [FromServices] CartService cartService) =>
        {
            var res = await cartService.RemoveItemAsync(body.VariantId);
            return Results.Ok(res);
        });

        app.MapPost("/api/cart/add-bundle", async ([FromBody] AddBundleBody body, [FromServices] CartService cartService) =>
        {
            var res = await cartService.AddStarterBundleAsync(body.BundleId);
            return res.Success ? Results.Ok(res) : Results.BadRequest(res);
        });
    }
}
