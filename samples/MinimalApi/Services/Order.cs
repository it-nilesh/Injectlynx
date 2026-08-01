namespace MinimalApi.Services;

public sealed record Order(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    string SampleName,
    string State);
