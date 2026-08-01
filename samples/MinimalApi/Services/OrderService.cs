namespace MinimalApi.Services;

public sealed class OrderService : IOrderService
{
    private string _sampleName = "uninitialized";
    private object? _state;

    public IClockService Clock { get; set; } = null!;

    public ILogger<OrderService>? Logger { get; set; }

    public void Initialize(string sampleName, object state)
    {
        _sampleName = sampleName;
        _state = state;
        Logger?.LogInformation("Initialized {Service} with state {State}.", sampleName, state);
    }

    public Task<Order?> GetAsync(Guid id) =>
        Task.FromResult<Order?>(new Order(
            id,
            "Sample order",
            Clock.GetUtcNow(),
            _sampleName,
            _state?.ToString() ?? "none"));
}
