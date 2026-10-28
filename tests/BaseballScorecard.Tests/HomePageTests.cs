using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests;

// Home's saved-games list and its Year and Team filters, against SCORECARD_TEST_DB.
// The run database is shared by every test, so each test checks only the games it saved.
[Collection(PostgresCollection.Name)]
public class HomePageTests : BunitContext
{
    private readonly GameService _games;
    private readonly RosterService _rosters;

    public HomePageTests(PostgresFixture fixture)
    {
        var factory = new FixtureDbFactory(fixture);
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(factory);
        Services.AddScoped<GameService>();
        _games = new GameService(factory);
        _rosters = new RosterService(factory);
    }

    private async Task<Roster> NewRosterAsync(string name, int year)
    {
        var roster = new Roster { TeamName = name, Year = year };
        Assert.Null(await _rosters.SaveAsync(roster));
        return roster;
    }

    private async Task<Game> NewGameAsync(Roster away, Roster home, DateOnly date)
    {
        var game = new Game
        {
            Year = away.Year, Date = date, AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = 9,
        };
        Assert.Null(await _games.CreateAsync(game));
        return game;
    }

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}";

    // The game links on the page, in page order.
    private static List<string> GameLinks(IRenderedComponent<Home> cut) =>
        cut.FindAll("ul.games a").Select(a => a.GetAttribute("href")!).ToList();

    private static List<string> Options(IRenderedComponent<Home> cut, string selectId) =>
        cut.FindAll($"#{selectId} option").Select(o => o.TextContent).ToList();

    private static Task Set(IRenderedComponent<Home> cut, string selector, string value) =>
        cut.InvokeAsync(() => cut.Find(selector).ChangeAsync(new ChangeEventArgs { Value = value }));

    [Fact]
    public void Home_shows_the_app_heading_and_the_rosters_and_new_game_links()
    {
        var cut = Render<Home>();

        Assert.Equal("Baseball Scorecard", cut.Find("h1").TextContent);
        Assert.Equal("Rosters", cut.Find("a[href='rosters']").TextContent);
        Assert.Equal("New game", cut.Find("a[href='games/new']").TextContent);
    }

    [Fact]
    public async Task With_no_games_home_says_no_games_yet()
    {
        var empty = new PostgresFixture();
        await empty.InitializeAsync();
        try
        {
            using var ctx = new BunitContext();
            ctx.Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(empty));
            ctx.Services.AddScoped<GameService>();

            var cut = ctx.Render<Home>();

            cut.WaitForAssertion(() => Assert.Equal("No games yet.", cut.Find("p.no-games").TextContent));
            Assert.Empty(cut.FindAll("ul.games"));
            Assert.Equal(["All"], Options(cut, "year-filter"));
            Assert.Equal(["All"], Options(cut, "team-filter"));
            Assert.NotNull(cut.Find("a[href='rosters']"));
            Assert.NotNull(cut.Find("a[href='games/new']"));
        }
        finally
        {
            await empty.DisposeAsync();
        }
    }

    [Fact]
    public async Task Lists_games_newest_date_first_showing_date_and_away_at_home_linking_to_the_game()
    {
        var cubs = await NewRosterAsync(Unique("Cubs"), 2026);
        var sox = await NewRosterAsync(Unique("Sox"), 2026);
        var older = await NewGameAsync(cubs, sox, new DateOnly(2026, 4, 2));
        var newer = await NewGameAsync(sox, cubs, new DateOnly(2026, 6, 9));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains($"games/{newer.Id}", GameLinks(cut)));
        var mine = GameLinks(cut).Where(h => h == $"games/{older.Id}" || h == $"games/{newer.Id}").ToList();
        Assert.Equal([$"games/{newer.Id}", $"games/{older.Id}"], mine);
        Assert.Equal(
            $"June 9, 2026 · {sox.TeamName} @ {cubs.TeamName}",
            cut.Find($"a[href='games/{newer.Id}']").TextContent);
        Assert.Empty(cut.FindAll("p.no-games"));
    }

    [Fact]
    public async Task Year_filter_shows_only_that_year_and_all_shows_every_year()
    {
        var cubs25 = await NewRosterAsync(Unique("Cubs"), 2025);
        var sox25 = await NewRosterAsync(Unique("Sox"), 2025);
        var cubs26 = await NewRosterAsync(Unique("Cubs"), 2026);
        var sox26 = await NewRosterAsync(Unique("Sox"), 2026);
        var in2025 = await NewGameAsync(cubs25, sox25, new DateOnly(2025, 5, 1));
        var in2026 = await NewGameAsync(cubs26, sox26, new DateOnly(2026, 5, 1));
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Contains("2025", Options(cut, "year-filter")));
        Assert.Equal("All", cut.Find("#year-filter option:checked").TextContent);

        await Set(cut, "#year-filter", "2025");

        cut.WaitForAssertion(() => Assert.DoesNotContain($"games/{in2026.Id}", GameLinks(cut)));
        Assert.Contains($"games/{in2025.Id}", GameLinks(cut));
        Assert.All(cut.FindAll("ul.games a"), a => Assert.Contains(", 2025 · ", a.TextContent));

        await Set(cut, "#year-filter", "");

        cut.WaitForAssertion(() => Assert.Contains($"games/{in2026.Id}", GameLinks(cut)));
        Assert.Contains($"games/{in2025.Id}", GameLinks(cut));
    }

    [Fact]
    public async Task Team_filter_matches_home_or_away_in_any_year_and_combines_with_year()
    {
        var team = Unique("Cubs");
        var cubs25 = await NewRosterAsync(team, 2025);
        var cubs26 = await NewRosterAsync(team, 2026);
        var sox25 = await NewRosterAsync(Unique("Sox"), 2025);
        var sox26 = await NewRosterAsync(Unique("Sox"), 2026);
        var mets26 = await NewRosterAsync(Unique("Mets"), 2026);
        var cubsAway25 = await NewGameAsync(cubs25, sox25, new DateOnly(2025, 5, 1));
        var cubsHome26 = await NewGameAsync(sox26, cubs26, new DateOnly(2026, 5, 1));
        var noCubs26 = await NewGameAsync(sox26, mets26, new DateOnly(2026, 5, 2));
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Contains(team, Options(cut, "team-filter")));
        Assert.Equal("All", cut.Find("#team-filter option:checked").TextContent);

        await Set(cut, "#team-filter", team);

        cut.WaitForAssertion(() => Assert.Equal([$"games/{cubsHome26.Id}", $"games/{cubsAway25.Id}"], GameLinks(cut)));
        Assert.DoesNotContain($"games/{noCubs26.Id}", GameLinks(cut));

        await Set(cut, "#year-filter", "2025");

        cut.WaitForAssertion(() => Assert.Equal([$"games/{cubsAway25.Id}"], GameLinks(cut)));
    }

    [Fact]
    public async Task Dropdowns_list_only_years_and_team_names_that_have_saved_games()
    {
        var team = Unique("Cubs");
        var cubs25 = await NewRosterAsync(team, 2025);
        await NewRosterAsync(team, 2026);
        var sox25 = await NewRosterAsync(Unique("Sox"), 2025);
        var idle = await NewRosterAsync(Unique("Idle"), 1872);
        await NewGameAsync(cubs25, sox25, new DateOnly(2025, 5, 1));

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains(team, Options(cut, "team-filter")));
        var years = Options(cut, "year-filter");
        var teams = Options(cut, "team-filter");
        Assert.Equal("All", years[0]);
        Assert.Contains("2025", years);
        Assert.DoesNotContain("1872", years);
        Assert.Equal("All", teams[0]);
        Assert.Single(teams, t => t == team);
        Assert.Contains(sox25.TeamName, teams);
        Assert.DoesNotContain(idle.TeamName, teams);
    }
}
