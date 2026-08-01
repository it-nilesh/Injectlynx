using Microsoft.AspNetCore.Hosting;
using WebApi.Infrastructure;

namespace WebApi.Services;

public sealed class OrderService(
    IOrderFormatter formatter,
    IAuditSink auditSink,
    IWebHostEnvironment environment) : IOrderService
{
    public async Task<OrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = new OrderSummary(id, formatter.Format(id), environment.EnvironmentName);
        await auditSink.RecordAsync("Order " + id + " read.", cancellationToken);
        return order;
    }
}
