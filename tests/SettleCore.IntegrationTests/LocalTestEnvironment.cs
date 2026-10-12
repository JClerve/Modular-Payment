using System.Runtime.CompilerServices;

namespace SettleCore.IntegrationTests;

internal static class LocalTestEnvironment
{
    // Existing behavior tests deliberately opt out; access tests override this with enabled configuration.
#pragma warning disable CA2255 // Test assembly initialization only, never included in application binaries.
    [ModuleInitializer]
    internal static void Initialize() => Environment.SetEnvironmentVariable("LocalApiAccess__Enabled", "false");
#pragma warning restore CA2255
}
