using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.Pitchers;

// Integration tests against SCORECARD_TEST_DB; each test makes its own game and rosters.
[Collection(PostgresCollection.Name)]
public class PitchingServiceTests(PostgresFixture fixture)
{
    private readonly PitchingService _pitching = new(new FixtureDbFactory(fixture));

    private sealed record Setup(Game Game, List<Player> Away, List<Player> Home);

    /// <summary>A game with 3 players on each roster.</summary>
    private async Task<Setup> NewGameAsync()
    {
        var f = new FixtureDbFactory(fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id };
        Assert.Null(await new GameService(f).CreateAsync(game));

        async Task<List<Player>> AddAsync(int rosterId)
        {
            var list = new List<Player>();
            for (var i = 1; i <= 3; i++)
            {
                var p = new Player { RosterId = rosterId, UniformNumber = $"{i}", FirstName = "First", LastName = $"Last{i}" };
                Assert.Null(await new PlayerService(f).SaveAsync(p));
                list.Add(p);
            }
            return list;
        }

        return new Setup(game, await AddAsync(away.Id), await AddAsync(home.Id));
    }

    private async Task<List<int>> PitcherIdsAsync(int gameId, int rosterId) =>
        (await _pitching.ListAsync(gameId)).Where(p => p.RosterId == rosterId).Select(p => p.PlayerId).ToList();

    [Fact]
    public async Task Pitchers_are_listed_per_team_in_the_order_they_were_added()
    {
        var s = await NewGameAsync();

        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[2].Id));
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.HomeRosterId, s.Home[1].Id));
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));

        Assert.Equal([s.Away[2].Id, s.Away[0].Id], await PitcherIdsAsync(s.Game.Id, s.Game.AwayRosterId));
        Assert.Equal([s.Home[1].Id], await PitcherIdsAsync(s.Game.Id, s.Game.HomeRosterId));
        Assert.All(await _pitching.ListAsync(s.Game.Id), p => Assert.Equal(PitcherDecision.None, p.Decision));
    }

    [Fact]
    public async Task A_player_already_in_the_table_is_refused()
    {
        var s = await NewGameAsync();
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));

        Assert.Equal(PitchingService.AlreadyPitchingMessage,
            await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));

        Assert.Equal([s.Away[0].Id], await PitcherIdsAsync(s.Game.Id, s.Game.AwayRosterId));
    }

    [Fact]
    public async Task A_player_from_another_roster_or_a_team_not_in_the_game_is_refused()
    {
        var s = await NewGameAsync();
        var other = await NewGameAsync();

        Assert.Equal(PitchingService.NotOnRosterMessage,
            await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Home[0].Id));
        Assert.Equal(PitchingService.TeamNotInGameMessage,
            await _pitching.AddAsync(s.Game.Id, other.Game.AwayRosterId, other.Away[0].Id));

        Assert.Empty(await _pitching.ListAsync(s.Game.Id));
    }

    [Fact]
    public async Task Removing_a_row_drops_only_that_pitcher_and_lets_them_be_added_again()
    {
        var s = await NewGameAsync();
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[1].Id));
        var first = (await _pitching.ListAsync(s.Game.Id))[0];

        await _pitching.RemoveAsync(s.Game.Id, first.Id);

        Assert.Equal([s.Away[1].Id], await PitcherIdsAsync(s.Game.Id, s.Game.AwayRosterId));
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));
        Assert.Equal([s.Away[1].Id, s.Away[0].Id], await PitcherIdsAsync(s.Game.Id, s.Game.AwayRosterId));
    }

    [Fact]
    public async Task Removing_a_row_of_another_game_changes_nothing()
    {
        var s = await NewGameAsync();
        var other = await NewGameAsync();
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));
        var row = Assert.Single(await _pitching.ListAsync(s.Game.Id));

        await _pitching.RemoveAsync(other.Game.Id, row.Id);

        Assert.Single(await _pitching.ListAsync(s.Game.Id));
    }

    [Theory]
    [InlineData(PitcherDecision.Win)]
    [InlineData(PitcherDecision.Loss)]
    [InlineData(PitcherDecision.Hold)]
    [InlineData(PitcherDecision.Save)]
    [InlineData(PitcherDecision.None)]
    public async Task Decision_saves_and_reloads(PitcherDecision decision)
    {
        var s = await NewGameAsync();
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.HomeRosterId, s.Home[0].Id));
        var row = Assert.Single(await _pitching.ListAsync(s.Game.Id));
        Assert.Null(await _pitching.SetDecisionAsync(s.Game.Id, row.Id, PitcherDecision.Win));

        Assert.Null(await _pitching.SetDecisionAsync(s.Game.Id, row.Id, decision));

        Assert.Equal(decision, Assert.Single(await _pitching.ListAsync(s.Game.Id)).Decision);
    }

    [Fact]
    public async Task An_unknown_decision_or_a_removed_row_is_refused()
    {
        var s = await NewGameAsync();
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));
        var row = Assert.Single(await _pitching.ListAsync(s.Game.Id));

        Assert.Equal(PitchingService.DecisionMessage, await _pitching.SetDecisionAsync(s.Game.Id, row.Id, (PitcherDecision)9));
        await _pitching.RemoveAsync(s.Game.Id, row.Id);
        Assert.Equal(PitchingService.PitcherRemovedMessage,
            await _pitching.SetDecisionAsync(s.Game.Id, row.Id, PitcherDecision.Win));
    }

    [Theory]
    [InlineData(PitcherDecision.None, "")]
    [InlineData(PitcherDecision.Win, "W")]
    [InlineData(PitcherDecision.Loss, "L")]
    [InlineData(PitcherDecision.Hold, "H")]
    [InlineData(PitcherDecision.Save, "S")]
    public void Decision_labels_are_the_scorecard_letters(PitcherDecision decision, string label) =>
        Assert.Equal(label, PitcherDecisions.Label(decision));

    // Pitching lines (#39): every stat is typed in, saved per pitcher row, blank allowed.

    private async Task<(Setup S, GamePitcher Row)> NewPitcherAsync()
    {
        var s = await NewGameAsync();
        Assert.Null(await _pitching.AddAsync(s.Game.Id, s.Game.AwayRosterId, s.Away[0].Id));
        return (s, Assert.Single(await _pitching.ListAsync(s.Game.Id)));
    }

    private async Task<string> StatTextAsync(int gameId, PitchingStat stat) =>
        PitchingStats.Text(Assert.Single(await _pitching.ListAsync(gameId)), stat);

    [Fact]
    public async Task A_new_pitcher_starts_with_every_stat_empty()
    {
        var (s, row) = await NewPitcherAsync();

        Assert.All(PitchingStats.All, stat => Assert.Equal("", PitchingStats.Text(row, stat)));
        Assert.Null(row.InningsPitchedOuts);
        Assert.Null(row.EarnedRuns);
    }

    [Theory]
    [InlineData("6", "6", 18)]
    [InlineData("6.1", "6.1", 19)]
    [InlineData("6.2", "6.2", 20)]
    [InlineData("0", "0", 0)]
    [InlineData("0.1", "0.1", 1)]
    [InlineData(" 7 ", "7", 21)]
    public async Task IP_takes_whole_innings_or_thirds_and_reloads(string typed, string shown, int outs)
    {
        var (s, row) = await NewPitcherAsync();

        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, PitchingStat.InningsPitched, typed));

        var reloaded = Assert.Single(await _pitching.ListAsync(s.Game.Id));
        Assert.Equal(outs, reloaded.InningsPitchedOuts);
        Assert.Equal(shown, PitchingStats.Text(reloaded, PitchingStat.InningsPitched));
    }

    [Theory]
    [InlineData("6.3")]
    [InlineData("-1")]
    [InlineData("-0.1")]
    [InlineData("6.5")]
    [InlineData("6.12")]
    [InlineData("6.")]
    [InlineData(".1")]
    [InlineData("six")]
    [InlineData("99999999999")]
    public async Task A_bad_IP_is_not_saved_and_shows_the_message(string typed)
    {
        var (s, row) = await NewPitcherAsync();
        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, PitchingStat.InningsPitched, "5.2"));

        Assert.Equal(PitchingService.InningsPitchedMessage,
            await _pitching.SetStatAsync(s.Game.Id, row.Id, PitchingStat.InningsPitched, typed));

        Assert.Equal("5.2", await StatTextAsync(s.Game.Id, PitchingStat.InningsPitched));
    }

    public static TheoryData<PitchingStat> CountStats => new(PitchingStats.All.Where(x => x != PitchingStat.InningsPitched));

    [Theory]
    [MemberData(nameof(CountStats))]
    public async Task Each_count_takes_a_whole_number_of_0_or_more_and_reloads(PitchingStat stat)
    {
        var (s, row) = await NewPitcherAsync();

        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, "0"));
        Assert.Equal("0", await StatTextAsync(s.Game.Id, stat));
        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, "27"));
        Assert.Equal("27", await StatTextAsync(s.Game.Id, stat));

        // Only this stat changed.
        var others = PitchingStats.All.Where(x => x != stat);
        var reloaded = Assert.Single(await _pitching.ListAsync(s.Game.Id));
        Assert.All(others, x => Assert.Equal("", PitchingStats.Text(reloaded, x)));
    }

    [Theory]
    [MemberData(nameof(CountStats))]
    public async Task A_bad_count_is_not_saved_and_shows_the_message(PitchingStat stat)
    {
        var (s, row) = await NewPitcherAsync();
        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, "4"));

        foreach (var typed in new[] { "-1", "1.5", "+3", "x", "99999999999" })
        {
            Assert.Equal(PitchingService.CountMessage, await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, typed));
        }

        Assert.Equal("4", await StatTextAsync(s.Game.Id, stat));
    }

    public static TheoryData<PitchingStat> AllStats => new(PitchingStats.All);

    [Theory]
    [MemberData(nameof(AllStats))]
    public async Task A_blank_box_is_saved_as_empty(PitchingStat stat)
    {
        var (s, row) = await NewPitcherAsync();
        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, "3"));

        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, "  "));
        Assert.Equal("", await StatTextAsync(s.Game.Id, stat));
        Assert.Null(await _pitching.SetStatAsync(s.Game.Id, row.Id, stat, null));
        Assert.Equal("", await StatTextAsync(s.Game.Id, stat));
    }

    [Fact]
    public async Task A_stat_for_a_removed_row_or_another_game_is_refused()
    {
        var (s, row) = await NewPitcherAsync();
        var other = await NewGameAsync();

        Assert.Equal(PitchingService.PitcherRemovedMessage,
            await _pitching.SetStatAsync(other.Game.Id, row.Id, PitchingStat.Strikeouts, "5"));
        await _pitching.RemoveAsync(s.Game.Id, row.Id);
        Assert.Equal(PitchingService.PitcherRemovedMessage,
            await _pitching.SetStatAsync(s.Game.Id, row.Id, PitchingStat.Strikeouts, "5"));
    }

    [Fact]
    public void Stats_are_labelled_in_scorecard_order() =>
        Assert.Equal(["IP", "BF", "BB", "SO", "H", "R", "ER"], PitchingStats.All.Select(PitchingStats.Label));
}
