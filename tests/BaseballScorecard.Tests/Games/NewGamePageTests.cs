using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Games;

// The /games/new form end to end against SCORECARD_TEST_DB: defaults, the year filter, validation and the save.
[Collection(PostgresCollection.Name)]
public class NewGamePageTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly RosterService _rosters;
    private readonly GatedDbFactory _gated;

    public NewGamePageTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _gated = new GatedDbFactory(fixture);
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(_gated);
        Services.AddScoped<GameService>();
        _rosters = new RosterService(new FixtureDbFactory(fixture));
    }

    // Holds every async context request until Gate completes, so a save can be left in flight on purpose.
    private sealed class GatedDbFactory(PostgresFixture fixture) : IDbContextFactory<ScorecardDbContext>
    {
        public TaskCompletionSource? Gate { get; set; }

        public ScorecardDbContext CreateDbContext() => fixture.CreateContext();

        public async Task<ScorecardDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (Gate is { } gate) await gate.Task;
            return fixture.CreateContext();
        }
    }

    private string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    private async Task<Roster> NewRosterAsync(string prefix, int year)
    {
        var roster = new Roster { TeamName = $"{prefix} {Guid.NewGuid():N}", Year = year };
        Assert.Null(await _rosters.SaveAsync(roster));
        return roster;
    }

    private async Task<int> CountGamesAsync()
    {
        await using var db = _fixture.CreateContext();
        return db.Games.Count();
    }

    // Find and fire in one dispatcher turn: the page re-renders after each async roster load.
    private static Task Set(IRenderedComponent<NewGame> cut, string selector, string value) =>
        cut.InvokeAsync(() => cut.Find(selector).ChangeAsync(new ChangeEventArgs { Value = value }));

    private static List<string> Options(IRenderedComponent<NewGame> cut, string selectId) =>
        cut.FindAll($"#{selectId} option").Select(o => o.TextContent).ToList();

    [Fact]
    public void Shows_the_defaults_and_batters_is_nine_and_read_only()
    {
        var cut = Render<NewGame>();

        Assert.Equal(DateTime.Today.Year.ToString(), cut.Find("#year").GetAttribute("value"));
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), cut.Find("#date").GetAttribute("value"));
        Assert.Equal("9", cut.Find("#innings").GetAttribute("value"));
        var batters = cut.Find("#batters");
        Assert.Equal("9", batters.GetAttribute("value"));
        Assert.True(batters.HasAttribute("readonly"));
        Assert.NotNull(cut.Find("#away"));
        Assert.NotNull(cut.Find("#home"));
    }

    [Fact]
    public async Task Team_dropdowns_list_only_rosters_for_the_selected_year_and_follow_year_changes()
    {
        var thisYear = await NewRosterAsync("Cubs", 2026);
        var lastYear = await NewRosterAsync("Sox", 2025);

        var cut = Render<NewGame>();
        await Set(cut, "#year", "2026");

        cut.WaitForAssertion(() => Assert.Contains(thisYear.TeamName, Options(cut, "away")));
        foreach (var select in new[] { "away", "home" })
        {
            Assert.Contains(thisYear.TeamName, Options(cut, select));
            Assert.DoesNotContain(lastYear.TeamName, Options(cut, select));
        }

        await Set(cut, "#year", "2025");

        cut.WaitForAssertion(() => Assert.Contains(lastYear.TeamName, Options(cut, "away")));
        foreach (var select in new[] { "away", "home" })
        {
            Assert.Contains(lastYear.TeamName, Options(cut, select));
            Assert.DoesNotContain(thisYear.TeamName, Options(cut, select));
        }
    }

    [Fact]
    public async Task Changing_the_year_clears_a_team_picked_from_the_old_year()
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var cut = Render<NewGame>();
        await Set(cut, "#year", "2026");
        cut.WaitForAssertion(() => Assert.Contains(away.TeamName, Options(cut, "away")));
        await Set(cut, "#away", away.Id.ToString());

        await Set(cut, "#year", "2025");

        cut.WaitForAssertion(() => Assert.Equal("0", cut.Find("#away").GetAttribute("value")));
    }

    [Fact]
    public async Task A_valid_save_creates_the_game_and_opens_its_page()
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var cut = Render<NewGame>();
        await Set(cut, "#year", "2026");
        cut.WaitForAssertion(() => Assert.Contains(home.TeamName, Options(cut, "home")));

        await Set(cut, "#date", "2026-04-02");
        await Set(cut, "#away", away.Id.ToString());
        await Set(cut, "#home", home.Id.ToString());
        await Set(cut, "#innings", "7");
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        cut.WaitForAssertion(() => Assert.Matches(@"/games/\d+$", CurrentUri));
        var id = int.Parse(CurrentUri[(CurrentUri.LastIndexOf('/') + 1)..]);
        var saved = await new GameService(new FixtureDbFactory(_fixture)).FindAsync(id);
        Assert.Equal(new DateOnly(2026, 4, 2), saved!.Date);
        Assert.Equal(away.Id, saved.AwayRosterId);
        Assert.Equal(home.Id, saved.HomeRosterId);
        Assert.Equal(7, saved.Innings);
        Assert.Equal(9, saved.Batters);
    }

    [Fact]
    public async Task Same_away_and_home_shows_a_message_and_saves_nothing()
    {
        var team = await NewRosterAsync("Cubs", 2026);
        var cut = Render<NewGame>();
        await Set(cut, "#year", "2026");
        cut.WaitForAssertion(() => Assert.Contains(team.TeamName, Options(cut, "away")));
        var startUri = CurrentUri;
        var before = await CountGamesAsync();

        await Set(cut, "#away", team.Id.ToString());
        await Set(cut, "#home", team.Id.ToString());
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        cut.WaitForAssertion(() => Assert.Equal(GameService.SameTeamMessage, cut.Find("[role=alert]").TextContent));
        Assert.Equal(startUri, CurrentUri);
        Assert.Equal(before, await CountGamesAsync());
    }

    [Fact]
    public async Task No_team_picked_shows_a_message_and_saves_nothing()
    {
        var cut = Render<NewGame>();
        var startUri = CurrentUri;
        var before = await CountGamesAsync();

        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        cut.WaitForAssertion(() => Assert.Equal(GameService.NoTeamMessage, cut.Find("[role=alert]").TextContent));
        Assert.Equal(startUri, CurrentUri);
        Assert.Equal(before, await CountGamesAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("2.5")]
    [InlineData("abc")]
    public async Task Innings_that_is_not_a_whole_number_of_at_least_one_shows_a_message_and_saves_nothing(string innings)
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var cut = Render<NewGame>();
        await Set(cut, "#year", "2026");
        cut.WaitForAssertion(() => Assert.Contains(home.TeamName, Options(cut, "home")));
        var startUri = CurrentUri;
        var before = await CountGamesAsync();

        await Set(cut, "#away", away.Id.ToString());
        await Set(cut, "#home", home.Id.ToString());
        await Set(cut, "#innings", innings);
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        cut.WaitForAssertion(() => Assert.Contains(GameService.InningsMessage, cut.Markup));
        Assert.Equal(startUri, CurrentUri);
        Assert.Equal(before, await CountGamesAsync());
    }

    private async Task<(IRenderedComponent<NewGame> Cut, Roster Away, Roster Home)> FilledFormAsync()
    {
        var away = await NewRosterAsync("Cubs", 2026);
        var home = await NewRosterAsync("Sox", 2026);
        var cut = Render<NewGame>();
        await Set(cut, "#year", "2026");
        cut.WaitForAssertion(() => Assert.Contains(home.TeamName, Options(cut, "home")));
        await Set(cut, "#away", away.Id.ToString());
        await Set(cut, "#home", home.Id.ToString());
        return (cut, away, home);
    }

    private async Task<int> CountGamesBetweenAsync(Roster away, Roster home)
    {
        await using var db = _fixture.CreateContext();
        return db.Games.Count(g => g.AwayRosterId == away.Id && g.HomeRosterId == home.Id);
    }

    [Fact]
    public async Task Save_is_disabled_while_a_save_is_running()
    {
        var (cut, _, _) = await FilledFormAsync();
        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
        var gate = _gated.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var save = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        cut.WaitForAssertion(() => Assert.True(cut.Find("button[type=submit]").HasAttribute("disabled")));
        gate.SetResult();
        await save;
    }

    [Fact]
    public async Task Submitting_twice_while_the_first_save_is_running_creates_exactly_one_game()
    {
        var (cut, away, home) = await FilledFormAsync();
        var gate = _gated.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        var second = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        gate.SetResult();
        await Task.WhenAll(first, second);

        cut.WaitForAssertion(() => Assert.Matches(@"/games/\d+$", CurrentUri));
        Assert.Equal(1, await CountGamesBetweenAsync(away, home));
    }

    [Fact]
    public async Task Save_is_enabled_again_after_a_save_fails_validation()
    {
        var cut = Render<NewGame>();

        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());

        cut.WaitForAssertion(() => Assert.Equal(GameService.NoTeamMessage, cut.Find("[role=alert]").TextContent));
        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
    }
}
