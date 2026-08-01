using MinimalApi.Services;

namespace MinimalApi.Handlers;

public sealed class GetOrderQueryHandler(IOrderService orders) : IRequestHandler<GetOrderQuery>
{
    public async Task<object?> HandleAsync(GetOrderQuery request) =>
        await orders.GetAsync(request.Id);
}
