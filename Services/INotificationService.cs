using KaryaJayaFootwear.Models;

namespace KaryaJayaFootwear.Services;

public interface INotificationService
{
    Task SendOrderCreatedAsync(Order order, CancellationToken ct = default);
    Task SendPaymentConfirmedAsync(Order order, CancellationToken ct = default);
    Task SendOrderDispatchedAsync(Order order, CancellationToken ct = default);
    Task<List<NotificationLog>> GetRecentLogsAsync(int take = 40);
}
