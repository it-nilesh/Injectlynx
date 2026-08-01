namespace WebApi.Services;

public sealed class CompactOrderFormatter : IOrderFormatter
{
    public string Format(Guid id) => "order-" + id.ToString("N")[..8];
}
