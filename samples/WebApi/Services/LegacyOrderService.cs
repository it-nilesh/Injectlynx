namespace WebApi.Services;

public sealed class LegacyOrderService : IOrderService
{
    public Task<OrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<OrderSummary?>(new OrderSummary(id, "legacy", "Excluded"));
}
