namespace WebApi.Services;

public sealed class LoggingOrderDecorator(
    IOrderService inner,
    ILogger<LoggingOrderDecorator> logger) : IOrderService
{
    public async Task<OrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading order {OrderId}.", id);
        return await inner.GetAsync(id, cancellationToken);
    }
}
