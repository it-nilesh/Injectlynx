namespace WebApi.Infrastructure;

public interface IAuditSink
{
    Task RecordAsync(string message, CancellationToken cancellationToken);
}
