using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Pitchers;

// The pitchers tables of the game page, end to end against SCORECARD_TEST_DB.
[Collection(PostgresCollection.Name)]
public class GamePitchersFormDbTests : BunitContext
{
    private readonly PostgresFixture _fixture;
    private readonly PitchingService _pitching;

    public GamePitchersFormDbTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<GameService>();
        Services.AddScoped<PlayerService>();
        Services.AddScoped<LineupService>();
        Services.AddScoped<LineScoreService>();
        Services.AddScoped<BattingGridService>();
        Services.AddScoped<PitchingService>();
        _pitching = new PitchingService(new FixtureDbFactory(fixture));
    }

    private sealed record Setup(Game Game, List<Player> Away, List<Player> Home);

    /// <summary>A game with 3 players on each roster, listed by uniform number 1–3.</summary>
    private async Task<Setup> NewGameAsync()
    {
        var f = new FixtureDbFactory(_fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id };
        Assert.Null(await new GameService(f).CreateAsync(game));

        async Task<List<Player>> AddAsync(int rosterId, string last)
        {
            var list = new List<Player>();
            for (var i = 1; i <= 3; i++)
            {
                var p = new Player { RosterId = rosterId, UniformNumber = $"{i}", FirstName = "First", LastName = $"{last}{i}" };
                Assert.Null(await new PlayerService(f).SaveAsync(p));
                list.Add(p);
            }
            return list;
        }

        return new Setup(game, await AddAsync(away.Id, "Cub"), await AddAsync(home.Id, "Sox"));
    }

    private IRenderedComponent<GamePage> RenderGame(int gameId)
    {
        var cut = Render<GamePage>(p => p.Add(x => x.Id, gameId));
        // Loaded: each pitchers table's picker lists the roster.
        cut.WaitForAssertion(() =>
        {
            foreach (var side in new[] { "away", "home" })
            {
                Assert.Equal(4, cut.FindAll($"#{side}-add-pitcher option").Count);
            }
        });
        return cut;
    }

    private static List<string> PitcherNames(IRenderedComponent<GamePage> cut, string side) =>
        cut.FindAll($"#{side}-pitchers tbody tr").Select(tr => tr.QuerySelector("td")!.TextContent.Trim()).ToList();

    private static void AddPitcher(IRenderedComponent<GamePage> cut, string side, Player p, int rowsAfter)
    {
        cut.Find($"#{side}-add-pitcher").Change($"{p.Id}");
        cut.WaitForAssertion(() => Assert.Equal(rowsAfter, cut.FindAll($"#{side}-pitchers tbody tr").Count));
    }

    [Fact]
    public async Task Each_team_has_a_pitchers_table_and_a_picker_listing_its_roster()
    {
        var s = await NewGameAsync();

        var cut = RenderGame(s.Game.Id);

        foreach (var (side, players) in new[] { ("away", s.Away), ("home", s.Home) })
        {
            Assert.Equal(["Pitcher", "Decision", ""],
                cut.FindAll($"#{side}-pitchers thead th").Select(th => th.TextContent.Trim()));
            Assert.Empty(cut.FindAll($"#{side}-pitchers tbody tr"));
            var options = cut.FindAll($"#{side}-add-pitcher option");
            Assert.Equal("Add pitcher", options[0].TextContent.Trim());
            Assert.Equal(players.Select(p => $"{p.Id}"), options.Skip(1).Select(o => o.GetAttribute("value")));
            Assert.Equal(players.Select(p => $"#{p.UniformNumber} First {p.LastName}"),
                options.Skip(1).Select(o => o.TextContent.Trim()));
        }
    }

    [Fact]
    public async Task Added_pitchers_list_in_the_order_they_were_added_and_after_a_reload()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);

        AddPitcher(cut, "away", s.Away[2], 1);
        AddPitcher(cut, "away", s.Away[0], 2);
        AddPitcher(cut, "home", s.Home[1], 1);

        string[] awayNames = ["#3 First Cub3", "#1 First Cub1"];
        Assert.Equal(awayNames, PitcherNames(cut, "away"));
        Assert.Equal("0", cut.Find("#away-add-pitcher").GetAttribute("value"));
        var reloaded = RenderGame(s.Game.Id);
        Assert.Equal(awayNames, PitcherNames(reloaded, "away"));
        Assert.Equal(["#2 First Sox2"], PitcherNames(reloaded, "home"));
    }

    [Fact]
    public async Task A_player_already_in_the_table_cannot_be_picked_again_even_after_a_reload()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        AddPitcher(cut, "away", s.Away[1], 1);

        var reloaded = RenderGame(s.Game.Id);

        var option = reloaded.Find($"#away-add-pitcher option[value='{s.Away[1].Id}']");
        Assert.True(option.HasAttribute("disabled"));
        Assert.False(reloaded.Find($"#away-add-pitcher option[value='{s.Away[0].Id}']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Adding_a_pitcher_already_added_elsewhere_shows_the_message_and_keeps_one_row()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));

        cut.Find("#away-add-pitcher").Change($"{s.Away[0].Id}");

        cut.WaitForAssertion(() => Assert.Equal(PitchingService.AlreadyPitchingMessage,
            cut.Find("#away-pitchers-error").TextContent.Trim()));
        Assert.Equal(["#1 First Cub1"], PitcherNames(cut, "away"));
        Assert.Single(await _pitching.ListAsync(s.Game.Id));
    }

    [Fact]
    public async Task Removing_a_row_drops_it_and_it_stays_gone_after_a_reload()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        AddPitcher(cut, "home", s.Home[0], 1);
        AddPitcher(cut, "home", s.Home[1], 2);

        cut.Find($"#home-pitcher-{s.Home[0].Id}-remove").Click();

        cut.WaitForAssertion(() => Assert.Equal(["#2 First Sox2"], PitcherNames(cut, "home")));
        Assert.False(cut.Find($"#home-add-pitcher option[value='{s.Home[0].Id}']").HasAttribute("disabled"));
        Assert.Equal(["#2 First Sox2"], PitcherNames(RenderGame(s.Game.Id), "home"));
    }

    [Fact]
    public async Task Decision_offers_blank_W_L_H_S_and_saves_and_reloads()
    {
        var s = await NewGameAsync();
        var cut = RenderGame(s.Game.Id);
        AddPitcher(cut, "away", s.Away[0], 1);
        AddPitcher(cut, "away", s.Away[1], 2);
        var id = $"#away-pitcher-{s.Away[0].Id}-decision";

        Assert.Equal(["", "W", "L", "H", "S"], cut.FindAll($"{id} option").Select(o => o.TextContent.Trim()));
        Assert.Equal($"{PitcherDecision.None}", cut.Find(id).GetAttribute("value"));

        cut.Find(id).Change($"{PitcherDecision.Win}");
        cut.WaitForAssertion(() => Assert.Equal($"{PitcherDecision.Win}", cut.Find(id).GetAttribute("value")));
        Assert.Equal(PitcherDecision.Win, (await _pitching.ListAsync(s.Game.Id))[0].Decision);

        var reloaded = RenderGame(s.Game.Id);
        Assert.Equal($"{PitcherDecision.Win}", reloaded.Find(id).GetAttribute("value"));
        Assert.Equal($"{PitcherDecision.None}", reloaded.Find($"#away-pitcher-{s.Away[1].Id}-decision").GetAttribute("value"));

        reloaded.Find(id).Change($"{PitcherDecision.None}");
        reloaded.WaitForAssertion(() => Assert.Equal($"{PitcherDecision.None}", reloaded.Find(id).GetAttribute("value")));
        Assert.Equal(PitcherDecision.None, (await _pitching.ListAsync(s.Game.Id))[0].Decision);
        Assert.Equal($"{PitcherDecision.None}", RenderGame(s.Game.Id).Find(id).GetAttribute("value"));
    }
}
