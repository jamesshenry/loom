using ModularPipelines.Models;

namespace Loom.Build.Tests.Unit;

public static class TestHelpers
{
    public static CommandResult EmptyCommandResult => new(
        "", "", "", "", new Dictionary<string, string?>(),
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero, 0
    );
}
