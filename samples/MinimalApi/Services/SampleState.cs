namespace MinimalApi.Services;

public sealed record SampleState(string Source)
{
    public override string ToString() => Source;
}
