using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.Lineups;

// Position changes against SCORECARD_TEST_DB; each test makes its own game and rosters.
[Collection(PostgresCollection.Name)]
public class PositionChangeServiceTests(PostgresFixture fixture)
{
    private readonly SubstitutionService _subs = new(new FixtureDbFactory(fixture));

    // Slot 1 bats the CF, slot 5 the RF.
    private static readonly Position[] NoDh =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.RightField,
        Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.Pitcher,
    ];

    /// <summary>A 9-inning game; the away roster has 12 players, the first 9 in the saved lineup, 10–12 on the bench.</summary>
    private sealed record Setup(Game Game, int RosterId, List<Player> Players)
    {
        public Player Bench(int i) => Players[8 + i];
    }

    private async Task<Setup> NewGameAsync()
    {
        var f = new FixtureDbFactory(fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game { Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id };
        Assert.Null(await new GameService(f).CreateAsync(game));
        var list = new List<Player>();
        for (var i = 1; i <= 12; i++)
        {
            var p = new Player { RosterId = away.Id, UniformNumber = $"{i}", FirstName = "First", LastName = $"Last{i}" };
            Assert.Null(await new PlayerService(f).SaveAsync(p));
            list.Add(p);
        }
        Assert.Null(await new LineupService(f).SaveAsync(new Lineup
        {
            GameId = game.Id,
            RosterId = away.Id,
            Slots = NoDh.Select((pos, i) => new LineupSlot { PlayerId = list[i].Id, Position = pos }).ToList(),
        }));
        return new Setup(game, away.Id, list);
    }

    private async Task<List<(int PlayerId, Position Position, int Inning)>> ChangesAsync(Setup s) =>
        (await _subs.ListPositionChangesAsync(s.Game.Id, s.RosterId)).Select(c => (c.PlayerId, c.Position, c.Inning)).ToList();

    [Fact]
    public async Task A_pinch_hitter_who_stays_in_and_a_starter_who_moves_are_both_saved()
    {
        var s = await NewGameAsync();
        var ph = s.Bench(1);
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 5, ph.Id, SubstitutionKind.PinchHitter, 7));

        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, ph.Id, Position.CenterField, 8));
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, Position.RightField, 8));

        Assert.Equal([(ph.Id, Position.CenterField, 8), (s.Players[0].Id, Position.RightField, 8)], await ChangesAsync(s));
    }

    [Theory]
    [InlineData(Position.PinchHitter)]
    [InlineData(Position.PinchRunner)]
    [InlineData((Position)0)]
    [InlineData((Position)13)]
    public async Task Pinch_hitter_pinch_runner_or_no_position_is_refused(Position position)
    {
        var s = await NewGameAsync();

        Assert.Equal(SubstitutionService.PositionChangeMessage,
            await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, position, 4));
        Assert.Empty(await ChangesAsync(s));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task An_inning_outside_the_game_is_refused(int inning)
    {
        var s = await NewGameAsync();

        Assert.Equal(SubstitutionService.InningMessage(9),
            await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, Position.RightField, inning));
        Assert.Empty(await ChangesAsync(s));
    }

    [Fact]
    public async Task A_change_before_the_player_came_in_is_refused()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 5, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));

        Assert.Equal(SubstitutionService.BeforeEnteredMessage(7),
            await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Bench(1).Id, Position.CenterField, 6));
        // The inning he came in is fine.
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Bench(1).Id, Position.CenterField, 7));
    }

    [Fact]
    public async Task A_player_not_in_the_game_in_that_inning_is_refused()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 5, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));

        // A bench player who never came in, and the starter after the pinch hitter replaced him.
        Assert.Equal(SubstitutionService.NotInGameMessage,
            await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Bench(2).Id, Position.CenterField, 8));
        Assert.Equal(SubstitutionService.NotInGameMessage,
            await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[4].Id, Position.CenterField, 7));
        // Before he left, the starter can still move.
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[4].Id, Position.CenterField, 6));
        Assert.Single(await ChangesAsync(s));
    }

    [Fact]
    public async Task A_second_change_for_the_same_player_in_the_same_inning_is_refused()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, Position.RightField, 8));

        Assert.Equal(SubstitutionService.SameInningMessage,
            await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, Position.LeftField, 8));
        Assert.Single(await ChangesAsync(s));
    }

    [Fact]
    public async Task Removing_a_change_deletes_only_that_row_of_that_game()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, Position.RightField, 8));
        var change = (await _subs.ListPositionChangesAsync(s.Game.Id, s.RosterId)).Single();

        await _subs.RemovePositionChangeAsync(s.Game.Id + 1000, change.Id);
        Assert.Single(await ChangesAsync(s));

        await _subs.RemovePositionChangeAsync(s.Game.Id, change.Id);
        Assert.Empty(await ChangesAsync(s));
    }

    [Fact]
    public async Task Removing_a_substitute_removes_his_position_changes()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 5, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Bench(1).Id, Position.CenterField, 8));
        Assert.Null(await _subs.AddPositionChangeAsync(s.Game.Id, s.RosterId, s.Players[0].Id, Position.RightField, 8));

        await _subs.RemoveAsync(s.Game.Id, (await _subs.ListAsync(s.Game.Id, s.RosterId)).Single().Id);

        Assert.Equal([(s.Players[0].Id, Position.RightField, 8)], await ChangesAsync(s));
    }
}

// The Pos column text of one player, from his first position and his changes; no database.
public class PositionTextTests
{
    private static PositionChange Change(int id, int playerId, Position position, int inning) =>
        new() { Id = id, PlayerId = playerId, Position = position, Inning = inning };

    [Fact]
    public void A_player_with_no_changes_shows_his_first_position()
    {
        Assert.Equal("CF", SubstitutionService.PositionText(new SlotPlayer(100, "CF", null, 1, null), []));
    }

    [Fact]
    public void Changes_follow_in_inning_order_and_other_players_changes_are_ignored()
    {
        var player = new SlotPlayer(200, "PH", 7, 7, 1);

        var text = SubstitutionService.PositionText(player,
            [Change(2, 200, Position.FirstBase, 9), Change(1, 200, Position.CenterField, 8), Change(3, 300, Position.Pitcher, 8)]);

        Assert.Equal("PH, CF, 1B", text);
    }

    [Fact]
    public void A_slot_with_no_position_picked_yet_shows_only_its_changes()
    {
        Assert.Equal("RF", SubstitutionService.PositionText(new SlotPlayer(100, "", null, 1, null),
            [Change(1, 100, Position.RightField, 3)]));
    }
}
