using WorkerService.Services;

namespace WorkerService;

public sealed class Worker(
    IHeartbeatService heartbeatService,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("{Heartbeat}", heartbeatService.CreateHeartbeat());
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
