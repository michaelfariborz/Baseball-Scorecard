using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.Lineups;

// Integration tests against SCORECARD_TEST_DB; each test makes its own game and rosters.
[Collection(PostgresCollection.Name)]
public class LineupServiceTests(PostgresFixture fixture)
{
    private readonly LineupService _lineups = new(new FixtureDbFactory(fixture));

    private static readonly Position[] NoDh =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.RightField,
        Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.Pitcher,
    ];

    private static readonly Position[] WithDh =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.DesignatedHitter,
        Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.RightField,
    ];

    private sealed record Setup(int GameId, int RosterId, List<Player> Players, int OtherRosterPlayerId);

    /// <summary>A game whose away roster has 11 players, plus one player on the home roster.</summary>
    private async Task<Setup> NewGameAsync()
    {
        var f = new FixtureDbFactory(fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id };
        Assert.Null(await new GameService(f).CreateAsync(game));

        var players = new PlayerService(f);
        var list = new List<Player>();
        for (var i = 1; i <= 11; i++)
        {
            var p = new Player { RosterId = away.Id, UniformNumber = $"{i}", FirstName = "First", LastName = $"Last{i}" };
            Assert.Null(await players.SaveAsync(p));
            list.Add(p);
        }
        var other = new Player { RosterId = home.Id, UniformNumber = "1", FirstName = "Other", LastName = "Team" };
        Assert.Null(await players.SaveAsync(other));
        return new Setup(game.Id, away.Id, list, other.Id);
    }

    private static Lineup Build(Setup s, Position[] positions, int? startingPitcherId = null) => new()
    {
        GameId = s.GameId,
        RosterId = s.RosterId,
        StartingPitcherId = startingPitcherId,
        Slots = positions.Select((pos, i) => new LineupSlot { PlayerId = s.Players[i].Id, Position = pos }).ToList(),
    };

    [Fact]
    public async Task Valid_lineup_saves_and_reloads_in_batting_order()
    {
        var s = await NewGameAsync();

        Assert.Null(await _lineups.SaveAsync(Build(s, NoDh)));

        var saved = await _lineups.FindAsync(s.GameId, s.RosterId);
        Assert.NotNull(saved);
        Assert.Equal(Enumerable.Range(1, 9), saved.Slots.Select(x => x.BattingOrder));
        Assert.Equal(s.Players.Take(9).Select(p => p.Id), saved.Slots.Select(x => x.PlayerId));
        Assert.Equal(NoDh, saved.Slots.Select(x => x.Position));
        Assert.Null(saved.StartingPitcherId);
    }

    [Fact]
    public async Task Saving_again_replaces_the_lineup()
    {
        var s = await NewGameAsync();
        Assert.Null(await _lineups.SaveAsync(Build(s, NoDh)));

        var changed = Build(s, NoDh);
        changed.Slots[0].PlayerId = s.Players[9].Id;
        (changed.Slots[1].Position, changed.Slots[2].Position) = (changed.Slots[2].Position, changed.Slots[1].Position);
        Assert.Null(await _lineups.SaveAsync(changed));

        var saved = (await _lineups.FindAsync(s.GameId, s.RosterId))!;
        Assert.Equal(9, saved.Slots.Count);
        Assert.Equal(s.Players[9].Id, saved.Slots[0].PlayerId);
        Assert.Equal([Position.LeftField, Position.Shortstop], saved.Slots.Skip(1).Take(2).Select(x => x.Position));
    }

    [Fact]
    public async Task No_lineup_yet_finds_nothing()
    {
        var s = await NewGameAsync();

        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, false)]
    [InlineData(9, true)]
    public async Task Empty_slot_is_rejected_naming_the_slot_and_nothing_is_saved(int slot, bool emptyPlayer)
    {
        var s = await NewGameAsync();
        var lineup = Build(s, NoDh);
        if (emptyPlayer)
        {
            lineup.Slots[slot - 1].PlayerId = 0;
        }
        else
        {
            lineup.Slots[slot - 1].Position = 0;
        }

        Assert.Equal(LineupService.EmptySlotMessage(slot), await _lineups.SaveAsync(lineup));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Same_player_twice_is_rejected_naming_the_slot_and_nothing_is_saved()
    {
        var s = await NewGameAsync();
        var lineup = Build(s, NoDh);
        lineup.Slots[6].PlayerId = lineup.Slots[2].PlayerId;

        Assert.Equal(LineupService.SamePlayerMessage(7, 3), await _lineups.SaveAsync(lineup));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Same_position_twice_is_rejected_naming_the_slot_and_nothing_is_saved()
    {
        var s = await NewGameAsync();
        var lineup = Build(s, NoDh);
        lineup.Slots[8].Position = lineup.Slots[0].Position;

        Assert.Equal(LineupService.SamePositionMessage(9, 1), await _lineups.SaveAsync(lineup));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Rejected_save_keeps_the_previous_lineup()
    {
        var s = await NewGameAsync();
        Assert.Null(await _lineups.SaveAsync(Build(s, NoDh)));
        var bad = Build(s, NoDh);
        bad.Slots[0].PlayerId = s.Players[9].Id;
        bad.Slots[4].PlayerId = 0;

        Assert.Equal(LineupService.EmptySlotMessage(5), await _lineups.SaveAsync(bad));

        Assert.Equal(s.Players[0].Id, (await _lineups.FindAsync(s.GameId, s.RosterId))!.Slots[0].PlayerId);
    }

    [Fact]
    public async Task Player_from_another_roster_is_rejected_naming_the_slot()
    {
        var s = await NewGameAsync();
        var lineup = Build(s, NoDh);
        lineup.Slots[3].PlayerId = s.OtherRosterPlayerId;

        Assert.Equal(LineupService.NotOnRosterMessage(4), await _lineups.SaveAsync(lineup));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Dh_lineup_saves_its_starting_pitcher()
    {
        var s = await NewGameAsync();

        Assert.Null(await _lineups.SaveAsync(Build(s, WithDh, s.Players[10].Id)));

        var saved = (await _lineups.FindAsync(s.GameId, s.RosterId))!;
        Assert.Equal(s.Players[10].Id, saved.StartingPitcherId);
        Assert.Equal(WithDh, saved.Slots.Select(x => x.Position));
    }

    [Fact]
    public async Task Dh_lineup_with_a_pitcher_batting_is_rejected_naming_the_slot()
    {
        var s = await NewGameAsync();
        var lineup = Build(s, WithDh, s.Players[10].Id);
        lineup.Slots[8].Position = Position.Pitcher;

        Assert.Equal(LineupService.PitcherWithDhMessage(9), await _lineups.SaveAsync(lineup));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Dh_lineup_without_a_starting_pitcher_is_rejected()
    {
        var s = await NewGameAsync();

        Assert.Equal(LineupService.StartingPitcherRequiredMessage, await _lineups.SaveAsync(Build(s, WithDh)));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Dh_lineup_with_a_starting_pitcher_in_the_batting_order_is_rejected()
    {
        var s = await NewGameAsync();

        Assert.Equal(
            LineupService.StartingPitcherBattingMessage,
            await _lineups.SaveAsync(Build(s, WithDh, s.Players[4].Id)));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Dh_lineup_with_a_starting_pitcher_from_another_roster_is_rejected()
    {
        var s = await NewGameAsync();

        Assert.Equal(
            LineupService.StartingPitcherRequiredMessage,
            await _lineups.SaveAsync(Build(s, WithDh, s.OtherRosterPlayerId)));
        Assert.Null(await _lineups.FindAsync(s.GameId, s.RosterId));
    }

    [Fact]
    public async Task Lineup_without_dh_drops_a_starting_pitcher_choice()
    {
        var s = await NewGameAsync();

        Assert.Null(await _lineups.SaveAsync(Build(s, NoDh, s.Players[10].Id)));

        Assert.Null((await _lineups.FindAsync(s.GameId, s.RosterId))!.StartingPitcherId);
    }
}
