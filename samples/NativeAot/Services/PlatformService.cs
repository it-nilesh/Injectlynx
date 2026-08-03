using System.Runtime.InteropServices;

namespace NativeAot.Services;

public sealed class PlatformService : IPlatformService
{
    public string GetDescription() => RuntimeInformation.FrameworkDescription;
}
