using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Players;

// The players section of the roster edit page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class RosterPlayersFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly PlayerService _players;

    public RosterPlayersFormDbTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<RosterService>();
        Services.AddScoped<PlayerService>();
        _players = new PlayerService(new FixtureDbFactory(fixture));
    }

    private async Task<int> NewRosterAsync()
    {
        var roster = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(new FixtureDbFactory(_fixture)).SaveAsync(roster));
        return roster.Id;
    }

    private async Task<Player> AddAsync(int rosterId, string number, string first, string last)
    {
        var player = new Player { RosterId = rosterId, UniformNumber = number, FirstName = first, LastName = last };
        Assert.Null(await _players.SaveAsync(player));
        return player;
    }

    private IRenderedComponent<RosterEdit> RenderRoster(int rosterId)
    {
        var cut = Render<RosterEdit>(p => p.Add(x => x.Id, rosterId));
        cut.WaitForElement("#player-form");
        return cut;
    }

    private static List<string> Listed(IRenderedComponent<RosterEdit> cut) =>
        cut.FindAll(".player").Select(e => e.TextContent.Trim()).ToList();

    private static void FillAndSubmit(IRenderedComponent<RosterEdit> cut, string number, string first, string last)
    {
        cut.Find("#player-number").Change(number);
        cut.Find("#player-first").Change(first);
        cut.Find("#player-last").Change(last);
        cut.Find("#player-form").Submit();
    }

    [Fact]
    public async Task Roster_page_lists_its_players_as_number_first_last_sorted_by_number()
    {
        var rosterId = await NewRosterAsync();
        await AddAsync(rosterId, "31", "Fergie", "Jenkins");
        await AddAsync(rosterId, "00", "Double", "Zero");
        await AddAsync(rosterId, "14", "Ernie", "Banks");
        await AddAsync(rosterId, "0", "Single", "Zero");

        var cut = RenderRoster(rosterId);

        Assert.Equal(["#0 Single Zero", "#00 Double Zero", "#14 Ernie Banks", "#31 Fergie Jenkins"], Listed(cut));
    }

    [Fact]
    public async Task Adding_a_player_through_the_form_saves_it_and_lists_it()
    {
        var rosterId = await NewRosterAsync();
        var cut = RenderRoster(rosterId);

        FillAndSubmit(cut, "14", "Ernie", "Banks");

        cut.WaitForAssertion(() => Assert.Equal(["#14 Ernie Banks"], Listed(cut)));
        var saved = Assert.Single(await _players.ListAsync(rosterId));
        Assert.Equal(("14", "Ernie", "Banks"), (saved.UniformNumber, saved.FirstName, saved.LastName));
        Assert.Equal("", cut.Find("#player-number").GetAttribute("value"));
    }

    [Theory]
    [InlineData("", "Ernie", "Banks", PlayerService.BlankFieldMessage)]
    [InlineData("14", "", "Banks", PlayerService.BlankFieldMessage)]
    [InlineData("14", "Ernie", " ", PlayerService.BlankFieldMessage)]
    [InlineData("1x", "Ernie", "Banks", PlayerService.NumberFormatMessage)]
    [InlineData("100", "Ernie", "Banks", PlayerService.NumberFormatMessage)]
    public async Task Invalid_player_shows_a_validation_message_and_saves_nothing(
        string number, string first, string last, string message)
    {
        var rosterId = await NewRosterAsync();
        var cut = RenderRoster(rosterId);

        FillAndSubmit(cut, number, first, last);

        cut.WaitForAssertion(() => Assert.Equal(message, cut.Find("#player-error").TextContent));
        Assert.Empty(Listed(cut));
        Assert.Empty(await _players.ListAsync(rosterId));
    }

    [Fact]
    public async Task Number_used_on_the_same_roster_shows_the_duplicate_message_and_saves_nothing()
    {
        var rosterId = await NewRosterAsync();
        await AddAsync(rosterId, "14", "Ernie", "Banks");
        var cut = RenderRoster(rosterId);

        FillAndSubmit(cut, "14", "Fergie", "Jenkins");

        cut.WaitForAssertion(() =>
            Assert.Equal(PlayerService.DuplicateNumberMessage, cut.Find("#player-error").TextContent));
        Assert.Single(await _players.ListAsync(rosterId));
    }

    [Fact]
    public async Task Editing_a_player_through_the_form_saves_the_change()
    {
        var rosterId = await NewRosterAsync();
        await AddAsync(rosterId, "14", "Ernie", "Banks");
        var cut = RenderRoster(rosterId);

        cut.Find(".edit-player").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("14", cut.Find("#player-number").GetAttribute("value"));
            Assert.Equal("Ernie", cut.Find("#player-first").GetAttribute("value"));
        });
        FillAndSubmit(cut, "31", "Fergie", "Jenkins");

        cut.WaitForAssertion(() => Assert.Equal(["#31 Fergie Jenkins"], Listed(cut)));
        var saved = Assert.Single(await _players.ListAsync(rosterId));
        Assert.Equal(("31", "Fergie", "Jenkins"), (saved.UniformNumber, saved.FirstName, saved.LastName));
    }

    [Fact]
    public async Task Removing_a_player_deletes_it()
    {
        var rosterId = await NewRosterAsync();
        await AddAsync(rosterId, "14", "Ernie", "Banks");
        await AddAsync(rosterId, "31", "Fergie", "Jenkins");
        var cut = RenderRoster(rosterId);

        cut.FindAll(".remove-player")[1].Click();

        cut.WaitForAssertion(() => Assert.Equal(["#14 Ernie Banks"], Listed(cut)));
        Assert.Equal("14", Assert.Single(await _players.ListAsync(rosterId)).UniformNumber);
    }

    [Fact]
    public void New_roster_page_has_no_players_section()
    {
        var cut = Render<RosterEdit>();

        Assert.Empty(cut.FindAll("#player-form"));
    }
}
