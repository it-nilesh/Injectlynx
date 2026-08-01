namespace MinimalApi.Handlers;

public interface IRequestHandler<TRequest>
{
    Task<object?> HandleAsync(TRequest request);
}
