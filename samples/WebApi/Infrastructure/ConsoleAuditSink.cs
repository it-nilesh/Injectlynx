namespace WebApi.Infrastructure;

public sealed class ConsoleAuditSink(ILogger<ConsoleAuditSink> logger) : IAuditSink
{
    public Task RecordAsync(string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("{AuditMessage}", message);
        return Task.CompletedTask;
    }
}
