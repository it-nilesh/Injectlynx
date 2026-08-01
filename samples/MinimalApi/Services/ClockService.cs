namespace MinimalApi.Services;

public sealed class ClockService : IClockService
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}
