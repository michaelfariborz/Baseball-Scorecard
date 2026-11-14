using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.Lineups;

// Integration tests against SCORECARD_TEST_DB; each test makes its own game and rosters.
[Collection(PostgresCollection.Name)]
public class SubstitutionServiceTests(PostgresFixture fixture)
{
    private readonly SubstitutionService _subs = new(new FixtureDbFactory(fixture));

    private static readonly Position[] NoDh =
    [
        Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.RightField,
        Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.Pitcher,
    ];

    /// <summary>A 9-inning game; the away roster has 12 players, the first 9 in the saved lineup, 10–12 on the bench.</summary>
    private sealed record Setup(Game Game, int RosterId, List<Player> Players, int OtherRosterPlayerId)
    {
        public Player Bench(int i) => Players[8 + i];
    }

    private async Task<Setup> NewGameAsync(bool saveLineup = true)
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
        for (var i = 1; i <= 12; i++)
        {
            var p = new Player { RosterId = away.Id, UniformNumber = $"{i}", FirstName = "First", LastName = $"Last{i}" };
            Assert.Null(await players.SaveAsync(p));
            list.Add(p);
        }
        var other = new Player { RosterId = home.Id, UniformNumber = "1", FirstName = "Other", LastName = "Team" };
        Assert.Null(await players.SaveAsync(other));
        if (saveLineup)
        {
            Assert.Null(await new LineupService(f).SaveAsync(new Lineup
            {
                GameId = game.Id,
                RosterId = away.Id,
                Slots = NoDh.Select((pos, i) => new LineupSlot { PlayerId = list[i].Id, Position = pos }).ToList(),
            }));
        }
        return new Setup(game, away.Id, list, other.Id);
    }

    [Fact]
    public async Task Added_substitutes_list_in_the_order_they_were_added()
    {
        var s = await NewGameAsync();

        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(2).Id, SubstitutionKind.PinchRunner, 7));
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 2, s.Bench(3).Id, SubstitutionKind.Defensive, 8));

        var list = await _subs.ListAsync(s.Game.Id, s.RosterId);
        Assert.Equal(
            [(6, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7), (6, s.Bench(2).Id, SubstitutionKind.PinchRunner, 7),
             (2, s.Bench(3).Id, SubstitutionKind.Defensive, 8)],
            list.Select(x => (x.BattingOrder, x.PlayerId, x.Kind, x.Inning)));
    }

    [Fact]
    public async Task A_starter_or_an_earlier_substitute_cannot_be_added_again()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));

        // A starter in another slot, the starter being replaced, and the substitute already in (or since replaced).
        Assert.Equal(SubstitutionService.AlreadyInGameMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Players[0].Id, SubstitutionKind.PinchHitter, 8));
        Assert.Equal(SubstitutionService.AlreadyInGameMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Players[5].Id, SubstitutionKind.Defensive, 8));
        Assert.Equal(SubstitutionService.AlreadyInGameMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 3, s.Bench(1).Id, SubstitutionKind.Defensive, 8));
        Assert.Single(await _subs.ListAsync(s.Game.Id, s.RosterId));
    }

    [Fact]
    public async Task A_designated_hitter_lineups_starting_pitcher_cannot_be_added()
    {
        var s = await NewGameAsync(saveLineup: false);
        var f = new FixtureDbFactory(fixture);
        Position[] withDh =
        [
            Position.CenterField, Position.Shortstop, Position.LeftField, Position.FirstBase, Position.DesignatedHitter,
            Position.ThirdBase, Position.Catcher, Position.SecondBase, Position.RightField,
        ];
        Assert.Null(await new LineupService(f).SaveAsync(new Lineup
        {
            GameId = s.Game.Id, RosterId = s.RosterId, StartingPitcherId = s.Bench(1).Id,
            Slots = withDh.Select((pos, i) => new LineupSlot { PlayerId = s.Players[i].Id, Position = pos }).ToList(),
        }));

        Assert.Equal(SubstitutionService.AlreadyInGameMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 5, s.Bench(1).Id, SubstitutionKind.PinchHitter, 4));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public async Task The_first_and_last_innings_of_the_game_are_accepted(int inning)
    {
        var s = await NewGameAsync();

        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(1).Id, SubstitutionKind.PinchHitter, inning));
        Assert.Equal([inning], (await _subs.ListAsync(s.Game.Id, s.RosterId)).Select(x => x.Inning));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task An_inning_outside_the_game_is_refused(int inning)
    {
        var s = await NewGameAsync();

        Assert.Equal(SubstitutionService.InningMessage(9),
            await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(1).Id, SubstitutionKind.PinchHitter, inning));
        Assert.Empty(await _subs.ListAsync(s.Game.Id, s.RosterId));
    }

    [Fact]
    public async Task Other_bad_input_is_refused_and_saves_nothing()
    {
        var s = await NewGameAsync();
        var noLineup = await NewGameAsync(saveLineup: false);

        Assert.Equal(SubstitutionService.NotOnRosterMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.OtherRosterPlayerId, SubstitutionKind.PinchHitter, 7));
        Assert.Equal(SubstitutionService.KindMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(1).Id, (SubstitutionKind)0, 7));
        Assert.Equal(SubstitutionService.SlotMessage,
            await _subs.AddAsync(s.Game.Id, s.RosterId, 10, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));
        Assert.Equal(SubstitutionService.SlotMessage,
            await _subs.AddAsync(noLineup.Game.Id, noLineup.RosterId, 6, noLineup.Bench(1).Id, SubstitutionKind.PinchHitter, 7));
        Assert.Equal(SubstitutionService.TeamNotInGameMessage,
            await _subs.AddAsync(noLineup.Game.Id, s.RosterId, 6, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));
        Assert.Empty(await _subs.ListAsync(s.Game.Id, s.RosterId));
    }

    [Fact]
    public async Task Removing_a_substitution_deletes_only_that_row_of_that_game()
    {
        var s = await NewGameAsync();
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(1).Id, SubstitutionKind.PinchHitter, 7));
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 6, s.Bench(2).Id, SubstitutionKind.PinchRunner, 7));
        var first = (await _subs.ListAsync(s.Game.Id, s.RosterId))[0];

        await _subs.RemoveAsync(s.Game.Id + 1000, first.Id);
        Assert.Equal(2, (await _subs.ListAsync(s.Game.Id, s.RosterId)).Count);

        await _subs.RemoveAsync(s.Game.Id, first.Id);
        Assert.Equal([s.Bench(2).Id], (await _subs.ListAsync(s.Game.Id, s.RosterId)).Select(x => x.PlayerId));

        // The removed player is on the bench again, so they can come in later.
        Assert.Null(await _subs.AddAsync(s.Game.Id, s.RosterId, 3, s.Bench(1).Id, SubstitutionKind.Defensive, 8));
    }
}

// Who holds a batting slot, worked out from its starter and substitutions; no database.
public class SlotPlayersTests
{
    private static readonly LineupSlot Starter = new() { BattingOrder = 6, PlayerId = 100, Position = Position.RightField };

    private static Substitution Sub(int id, int playerId, SubstitutionKind kind, int inning) =>
        new() { Id = id, BattingOrder = 6, PlayerId = playerId, Kind = kind, Inning = inning };

    [Fact]
    public void A_slot_with_no_substitutes_is_its_starter_in_every_inning()
    {
        var players = SubstitutionService.SlotPlayers(Starter, []);

        Assert.Equal([(100, "RF", (int?)null)], players.Select(p => (p.PlayerId, p.Position, p.Inning)));
        Assert.Equal(100, SubstitutionService.OwnerAt(players, 1).PlayerId);
        Assert.Equal(100, SubstitutionService.OwnerAt(players, 9).PlayerId);
    }

    [Fact]
    public void A_pinch_hitter_owns_his_inning_and_later()
    {
        var players = SubstitutionService.SlotPlayers(Starter, [Sub(1, 200, SubstitutionKind.PinchHitter, 7)]);

        Assert.Equal([(100, "RF", (int?)null), (200, "PH", 7)], players.Select(p => (p.PlayerId, p.Position, p.Inning)));
        Assert.Equal(100, SubstitutionService.OwnerAt(players, 6).PlayerId);
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 7).PlayerId);
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 9).PlayerId);
    }

    [Fact]
    public void A_pinch_runner_leaves_his_inning_with_the_batter_and_owns_the_next()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 200, SubstitutionKind.PinchHitter, 7), Sub(2, 300, SubstitutionKind.PinchRunner, 7)]);

        Assert.Equal([100, 200, 300], players.Select(p => p.PlayerId));
        Assert.Equal("PR", players[2].Position);
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 7).PlayerId);
        Assert.Equal(300, SubstitutionService.OwnerAt(players, 8).PlayerId);
    }

    [Fact]
    public void A_defensive_sub_takes_the_starters_position()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 200, SubstitutionKind.PinchHitter, 7), Sub(2, 300, SubstitutionKind.Defensive, 8)]);

        Assert.Equal(["RF", "PH", "RF"], players.Select(p => p.Position));
        Assert.Equal(300, SubstitutionService.OwnerAt(players, 8).PlayerId);
    }

    [Fact]
    public void A_defensive_sub_takes_the_position_the_player_he_replaces_holds_in_his_inning()
    {
        PositionChange Change(int id, int playerId, Position position, int inning) =>
            new() { Id = id, PlayerId = playerId, Position = position, Inning = inning };

        // The RF starter moves to CF from the 5th; a defensive sub for him in the 7th plays CF.
        var players = SubstitutionService.SlotPlayers(Starter, [Sub(1, 300, SubstitutionKind.Defensive, 7)],
            [Change(1, 100, Position.CenterField, 5), Change(2, 999, Position.Pitcher, 5)]);
        Assert.Equal(["RF", "CF"], players.Select(p => p.Position));

        // A PH who stays in at 1B from the 8th; a defensive sub for him in the 9th plays 1B.
        players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 200, SubstitutionKind.PinchHitter, 7), Sub(2, 300, SubstitutionKind.Defensive, 9)],
            [Change(1, 200, Position.FirstBase, 8)]);
        Assert.Equal(["RF", "PH", "1B"], players.Select(p => p.Position));
    }

    [Fact]
    public void Substitutes_list_in_the_order_they_took_the_slot_whatever_order_they_were_added()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 300, SubstitutionKind.Defensive, 8), Sub(2, 200, SubstitutionKind.PinchHitter, 5)]);

        Assert.Equal([100, 200, 300], players.Select(p => p.PlayerId));
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 7).PlayerId);
    }

    [Fact]
    public void A_defensive_sub_for_a_pinch_hitter_in_his_inning_leaves_that_at_bat_with_the_pinch_hitter()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 200, SubstitutionKind.PinchHitter, 7), Sub(2, 300, SubstitutionKind.Defensive, 7)]);

        Assert.Equal([100, 200, 300], players.Select(p => p.PlayerId));
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 7).PlayerId);
        Assert.Equal(300, SubstitutionService.OwnerAt(players, 8).PlayerId);
    }

    [Fact]
    public void A_defensive_sub_for_a_pinch_runner_in_his_inning_owns_the_next_inning()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 200, SubstitutionKind.PinchRunner, 7), Sub(2, 300, SubstitutionKind.Defensive, 7)]);

        Assert.Equal([100, 200, 300], players.Select(p => p.PlayerId));
        Assert.Equal(100, SubstitutionService.OwnerAt(players, 7).PlayerId);
        Assert.Equal(300, SubstitutionService.OwnerAt(players, 8).PlayerId);
    }

    [Fact]
    public void Substitutes_order_by_the_inning_they_came_in_even_when_a_later_one_was_added_first()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
            [Sub(1, 200, SubstitutionKind.PinchHitter, 6), Sub(2, 300, SubstitutionKind.PinchRunner, 5)]);

        Assert.Equal([100, 300, 200], players.Select(p => p.PlayerId));
        Assert.Equal(100, SubstitutionService.OwnerAt(players, 5).PlayerId);
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 6).PlayerId);
        Assert.Equal(200, SubstitutionService.OwnerAt(players, 9).PlayerId);
    }

    [Fact]
    public void A_third_sub_in_the_same_inning_never_takes_back_an_at_bat_the_slot_already_had()
    {
        var players = SubstitutionService.SlotPlayers(Starter,
        [
            Sub(1, 200, SubstitutionKind.PinchRunner, 7), Sub(2, 300, SubstitutionKind.Defensive, 7),
            Sub(3, 400, SubstitutionKind.Defensive, 7),
        ]);

        Assert.Equal([100, 200, 300, 400], players.Select(p => p.PlayerId));
        Assert.Equal(100, SubstitutionService.OwnerAt(players, 7).PlayerId);
        Assert.Equal(400, SubstitutionService.OwnerAt(players, 8).PlayerId);
    }
}
