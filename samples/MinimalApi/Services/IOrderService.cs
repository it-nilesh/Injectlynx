namespace MinimalApi.Services;

public interface IOrderService
{
    Task<Order?> GetAsync(Guid id);
}
