namespace KaryaJayaFootwear.Services;

public sealed class OrderLifecycleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderLifecycleBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public OrderLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderLifecycleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        // Run once shortly after startup, then every 15 minutes
        await RunTickAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunTickAsync(stoppingToken);
        }
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var fulfillment = scope.ServiceProvider.GetRequiredService<OrderFulfillmentService>();
            var result = await fulfillment.RunOrderLifecycleCheckAsync(ct);

            if (result.ExpiredOrdersCount > 0 || result.FlaggedTempoNearDueCount > 0)
            {
                _logger.LogInformation(
                    "OrderLifecycle tick: {ExpiredCount} expired ({ReleasedPairs} pairs released), {FlaggedCount} B2B Tempo orders flagged near due.",
                    result.ExpiredOrdersCount,
                    result.ReleasedPairsCount,
                    result.FlaggedTempoNearDueCount);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while running OrderLifecycleBackgroundService tick.");
        }
    }
}
