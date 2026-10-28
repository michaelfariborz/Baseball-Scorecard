using System.Reflection;
using BaseballScorecard.Components;
using BaseballScorecard.Components.Layout;
using BaseballScorecard.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BaseballScorecard.Tests;

public class MainLayoutTests : BunitContext
{
    [Fact]
    public void Layout_shows_a_Home_link_to_root_above_the_page_body()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p id=\"body\">content</p>"))));

        var home = cut.Find("a[href='/']");
        Assert.Equal("Home", home.TextContent);
        Assert.True(cut.Markup.IndexOf("href=\"/\"", StringComparison.Ordinal) < cut.Markup.IndexOf("id=\"body\"", StringComparison.Ordinal));
        Assert.Equal("content", cut.Find("#body").TextContent);
    }

    [Theory]
    [InlineData(typeof(Components.Pages.Rosters))]
    [InlineData(typeof(RosterEdit))]
    [InlineData(typeof(NewGame))]
    [InlineData(typeof(GamePage))]
    public void Pages_use_the_shared_layout(Type page)
    {
        Assert.Null(page.GetCustomAttribute<LayoutAttribute>());
        Assert.NotNull(page.GetCustomAttributes<RouteAttribute>().FirstOrDefault());
    }

    [Fact]
    public void Routed_page_is_wrapped_in_the_layout()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<Routes>();

        Assert.Equal("Home", cut.Find("a[href='/']").TextContent);
        Assert.Equal("Baseball Scorecard", cut.Find("h1").TextContent);
    }
}
