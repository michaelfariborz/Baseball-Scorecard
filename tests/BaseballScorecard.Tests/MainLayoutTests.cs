using System.Reflection;
using BaseballScorecard.Components;
using BaseballScorecard.Components.Layout;
using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests;

// Home, the routed page here, reads games on load, so it needs the run database.
[Collection(PostgresCollection.Name)]
public class MainLayoutTests : BunitContext
{
    public MainLayoutTests(PostgresFixture fixture)
    {
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
    }

    [Fact]
    public void Layout_shows_a_Home_link_to_root_above_the_page_body()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p id=\"body\">content</p>"))));

        var home = cut.Find("a[href='/']");
        Assert.Equal("Home", home.TextContent);
        Assert.True(cut.Markup.IndexOf("href=\"/\"", StringComparison.Ordinal) < cut.Markup.IndexOf("id=\"body\"", StringComparison.Ordinal));
        Assert.Equal("content", cut.Find("#body").TextContent);
    }

    private IRenderedComponent<MainLayout> RenderAt(string uri)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
        return Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p>content</p>"))));
    }

    [Fact]
    public void Header_shows_the_app_name_and_nav_links_to_home_rosters_and_new_game()
    {
        var cut = RenderAt("/");

        var header = cut.Find("header.app-header");
        Assert.Equal("Baseball Scorecard", header.QuerySelector(".brand")!.TextContent);
        var links = header.QuerySelectorAll("nav.app-nav a").Select(a => (a.GetAttribute("href"), a.TextContent.Trim())).ToList();
        Assert.Equal([("/", "Home"), ("rosters", "Rosters"), ("games/new", "New game")], links);
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("rosters", "rosters")]
    [InlineData("rosters/3/edit", "rosters")]
    [InlineData("rosters/new", "rosters")]
    [InlineData("games/new", "games/new")]
    public void Only_the_current_page_link_is_marked_aria_current(string uri, string current)
    {
        var cut = RenderAt(uri);

        var marked = cut.FindAll("nav.app-nav a[aria-current='page']").Select(a => a.GetAttribute("href")).ToList();
        Assert.Equal([current], marked);
    }

    [Fact]
    public void No_nav_link_is_marked_on_a_game_page()
    {
        var cut = RenderAt("games/5");

        Assert.Empty(cut.FindAll("nav.app-nav a[aria-current]"));
    }

    [Fact]
    public void Page_body_sits_in_a_main_landmark()
    {
        var cut = RenderAt("/");

        Assert.Equal("content", cut.Find("main p").TextContent);
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
