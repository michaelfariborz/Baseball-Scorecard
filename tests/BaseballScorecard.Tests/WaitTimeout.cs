using System.Runtime.CompilerServices;

namespace BaseballScorecard.Tests;

// Page renders follow real database calls; bUnit's 1 s default wait is too short on a loaded machine.
internal static class WaitTimeout
{
    [ModuleInitializer]
    internal static void Init() => Bunit.BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(10);
}
