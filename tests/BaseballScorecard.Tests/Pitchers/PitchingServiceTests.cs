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
}
