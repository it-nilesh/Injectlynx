namespace WorkerService.Services;

public sealed class HeartbeatService : IHeartbeatService
{
    public string CreateHeartbeat() => $"Worker heartbeat at {DateTimeOffset.UtcNow:O}";
}
