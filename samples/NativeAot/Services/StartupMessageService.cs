namespace NativeAot.Services;

public sealed class StartupMessageService(
    IClockService clockService,
    IPlatformService platformService) : IStartupMessageService
{
    public string CreateMessage() => clockService.GetMessage() + " on " + platformService.GetDescription();
}
