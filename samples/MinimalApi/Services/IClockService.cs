namespace MinimalApi.Services;

public interface IClockService
{
    DateTimeOffset GetUtcNow();
}
