namespace WebApi.Services.Internal;

public sealed class InternalOrderService : WebApi.Services.IOrderService
{
    public Task<WebApi.Services.OrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<WebApi.Services.OrderSummary?>(new WebApi.Services.OrderSummary(id, "internal", "Excluded"));
}
