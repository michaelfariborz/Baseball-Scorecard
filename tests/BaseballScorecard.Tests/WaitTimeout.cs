using System.Runtime.CompilerServices;
using Bunit;

namespace BaseballScorecard.Tests;

// Page tests wait on renders that follow real database calls. bUnit's default 1 s wait is too short
// on a loaded machine (the merge gate's run), so every WaitFor* gets this longer default.
internal static class WaitTimeout
{
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(10);

    [ModuleInitializer]
    internal static void Init() => BunitContext.DefaultWaitTimeout = Default;
}

public class WaitTimeoutTests
{
    [Fact]
    public void Page_tests_wait_long_enough_for_a_loaded_machine() =>
        Assert.Equal(TimeSpan.FromSeconds(10), BunitContext.DefaultWaitTimeout);
}
