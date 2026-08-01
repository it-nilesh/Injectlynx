namespace WebApi.Services;

public interface IOrderService
{
    Task<OrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken);
}
