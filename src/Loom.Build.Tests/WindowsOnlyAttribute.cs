using System.Runtime.InteropServices;

namespace Loom.Build.Tests;

public class WindowsOnlyAttribute() : SkipAttribute("This test is only supported on Windows")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }
}
