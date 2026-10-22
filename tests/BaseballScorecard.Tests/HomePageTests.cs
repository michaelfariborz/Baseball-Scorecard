using BaseballScorecard.Components.Pages;
using Bunit;

namespace BaseballScorecard.Tests;

public class HomePageTests : BunitContext
{
    [Fact]
    public void Home_shows_the_app_heading()
    {
        var cut = Render<Home>();

        Assert.Equal("Baseball Scorecard", cut.Find("h1").TextContent);
    }
}
