using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;

namespace BaseballScorecard.Tests.BattingGrids;

// Integration tests against SCORECARD_TEST_DB; each test makes its own game and rosters.
[Collection(PostgresCollection.Name)]
public class BattingGridServiceTests(PostgresFixture fixture)
{
    private readonly BattingGridService _grid = new(new FixtureDbFactory(fixture));

    private async Task<Game> NewGameAsync(int innings = 9)
    {
        var f = new FixtureDbFactory(fixture);
        var away = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        var home = new Roster { TeamName = $"Sox {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(f).SaveAsync(away));
        Assert.Null(await new RosterService(f).SaveAsync(home));
        var game = new Game
        {
            Year = 2026, Date = new DateOnly(2026, 4, 2), AwayRosterId = away.Id, HomeRosterId = home.Id, Innings = innings,
        };
        Assert.Null(await new GameService(f).CreateAsync(game));
        return game;
    }

    private async Task<Dictionary<(int RosterId, int Slot, int Inning), string>> NotesAsync(int gameId) =>
        (await _grid.ListNotesAsync(gameId)).ToDictionary(n => (n.RosterId, n.BattingOrder, n.Inning), n => n.Note);

    [Fact]
    public async Task Notes_save_per_team_slot_and_inning_and_reload()
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "6-3"));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 9, 9, " BB "));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "K"));

        var notes = await NotesAsync(g.Id);
        Assert.Equal(3, notes.Count);
        Assert.Equal("6-3", notes[(g.AwayRosterId, 1, 1)]);
        Assert.Equal("BB", notes[(g.AwayRosterId, 9, 9)]);
        Assert.Equal("K", notes[(g.HomeRosterId, 1, 1)]);
    }

    [Fact]
    public async Task Saving_a_cell_again_replaces_its_note()
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 2, 3, "1B"));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 2, 3, "2B"));

        Assert.Equal("2B", Assert.Single(await NotesAsync(g.Id)).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Blank_note_clears_the_cell(string? blank)
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 4, 2, "K"));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 4, 2, blank));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 4, 2, blank));

        Assert.Empty(await NotesAsync(g.Id));
    }

    [Fact]
    public async Task Note_of_20_characters_saves_and_21_is_refused()
    {
        var g = await NewGameAsync();
        var twenty = new string('x', 20);

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, twenty));
        Assert.Equal(BattingGridService.NoteTooLongMessage,
            await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, twenty + "y"));

        Assert.Equal(twenty, Assert.Single(await NotesAsync(g.Id)).Value);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 8)]
    public async Task Note_outside_the_slots_or_innings_is_refused(int slot, int inning)
    {
        var g = await NewGameAsync(innings: 7);

        Assert.NotNull(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, slot, inning, "K"));

        Assert.Empty(await NotesAsync(g.Id));
    }

    [Fact]
    public async Task Hits_and_lob_save_per_team_and_inning_and_blank_clears()
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveHitsAsync(g.Id, g.AwayRosterId, 1, "2"));
        Assert.Null(await _grid.SaveLobAsync(g.Id, g.AwayRosterId, 1, "0"));
        Assert.Null(await _grid.SaveLobAsync(g.Id, g.HomeRosterId, 9, " 3 "));

        var saved = (await _grid.ListHitsLobAsync(g.Id)).ToDictionary(t => (t.RosterId, t.Inning));
        Assert.Equal(2, saved.Count);
        Assert.Equal(2, saved[(g.AwayRosterId, 1)].Hits);
        Assert.Equal(0, saved[(g.AwayRosterId, 1)].Lob);
        Assert.Null(saved[(g.HomeRosterId, 9)].Hits);
        Assert.Equal(3, saved[(g.HomeRosterId, 9)].Lob);

        Assert.Null(await _grid.SaveHitsAsync(g.Id, g.AwayRosterId, 1, ""));
        Assert.Equal(0, Assert.Single(await _grid.ListHitsLobAsync(g.Id), t => t.RosterId == g.AwayRosterId).Lob);
        Assert.Null(await _grid.SaveLobAsync(g.Id, g.AwayRosterId, 1, " "));
        Assert.DoesNotContain(await _grid.ListHitsLobAsync(g.Id), t => t.RosterId == g.AwayRosterId);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("two")]
    [InlineData("1.5")]
    [InlineData("+2")]
    public async Task Bad_hits_or_lob_is_refused_and_changes_nothing(string bad)
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveHitsAsync(g.Id, g.AwayRosterId, 1, "2"));
        Assert.Null(await _grid.SaveLobAsync(g.Id, g.AwayRosterId, 1, "1"));

        Assert.Equal(BattingGridService.HitsLobMessage, await _grid.SaveHitsAsync(g.Id, g.AwayRosterId, 1, bad));
        Assert.Equal(BattingGridService.HitsLobMessage, await _grid.SaveLobAsync(g.Id, g.AwayRosterId, 1, bad));

        var row = Assert.Single(await _grid.ListHitsLobAsync(g.Id));
        Assert.Equal(2, row.Hits);
        Assert.Equal(1, row.Lob);
    }

    [Fact]
    public async Task Cells_outside_the_innings_or_for_another_team_are_refused()
    {
        var g = await NewGameAsync(innings: 7);
        var other = await NewGameAsync();

        Assert.Equal(BattingGridService.InningMessage, await _grid.SaveHitsAsync(g.Id, g.AwayRosterId, 8, "1"));
        Assert.Equal(BattingGridService.TeamNotInGameMessage, await _grid.SaveLobAsync(g.Id, other.HomeRosterId, 1, "1"));
        Assert.Equal(BattingGridService.TeamNotInGameMessage, await _grid.SaveNoteAsync(g.Id, other.AwayRosterId, 1, 1, "K"));

        Assert.Empty(await _grid.ListHitsLobAsync(g.Id));
        Assert.Empty(await NotesAsync(g.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Bases_reached_saves_with_the_note_and_reloads(int bases)
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 3, 2, "1B", bases));

        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("1B", bases), (note.Note, note.BasesReached));
    }

    [Fact]
    public async Task Bases_reached_defaults_to_none_when_not_given()
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 3, 2, "K"));

        Assert.Equal(0, Assert.Single(await _grid.ListNotesAsync(g.Id)).BasesReached);
    }

    [Fact]
    public async Task Saving_again_replaces_bases_reached_and_clearing_removes_it()
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "2B", 2));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "2B", 3));
        Assert.Equal(3, Assert.Single(await _grid.ListNotesAsync(g.Id)).BasesReached);

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, null));
        Assert.Empty(await _grid.ListNotesAsync(g.Id));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "BB"));
        Assert.Equal(0, Assert.Single(await _grid.ListNotesAsync(g.Id)).BasesReached);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task Bases_reached_outside_0_to_4_is_refused(int bases)
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "1B", 1));

        Assert.Equal(BattingGridService.BasesReachedMessage,
            await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "HR", bases));

        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("1B", 1), (note.Note, note.BasesReached));
    }

    [Theory]
    [InlineData("GO", "4-6-3", "DP")]
    [InlineData("FO", "8", "")]
    [InlineData("SF", "9", null)]
    [InlineData("FC", "6-4", "")]
    [InlineData("GO", "5-4-3", "TP")]
    [InlineData("E", "6", "")]
    [InlineData("GO", "", "")]
    public async Task Fielders_and_double_or_triple_play_save_with_the_result_and_reload(string code, string fielders, string? multiOut)
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 3, 2, code, 0, $" {fielders} ", multiOut));

        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal((code, fielders, multiOut ?? ""), (note.Note, note.Fielders, note.MultiOut));
    }

    [Theory]
    [InlineData("GO", "463", "4-6-3")]
    [InlineData("GO", "4-6-3", "4-6-3")]
    [InlineData("FO", "46-3", "4-6-3")]
    [InlineData("SF", "8", "8")]
    [InlineData("FC", " 63 ", "6-3")]
    [InlineData("GO", "1234567891", "1-2-3-4-5-6-7-8-9-1")]
    public async Task Fielders_typed_with_or_without_hyphens_save_hyphen_joined(string code, string typed, string saved)
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, code, 0, typed));

        Assert.Equal(saved, Assert.Single(await _grid.ListNotesAsync(g.Id)).Fielders);
    }

    [Theory]
    [InlineData("GO", "12345678912")]
    [InlineData("GO", "0")]
    [InlineData("GO", "10")]
    [InlineData("GO", "6-")]
    [InlineData("GO", "-3")]
    [InlineData("GO", "6--3")]
    [InlineData("GO", "6 3")]
    [InlineData("GO", "4a3")]
    [InlineData("FO", "SS")]
    [InlineData("FC", "6/4")]
    [InlineData("SF", "1-2-3-4-5-6-7-8-9-1-2")]
    public async Task Fielders_other_than_positions_1_to_9_joined_by_hyphens_are_refused(string code, string fielders)
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "GO", 0, "6-3"));

        Assert.Equal(BattingGridService.FieldersMessage,
            await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, code, 0, fielders));

        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("GO", "6-3"), (note.Note, note.Fielders));
    }

    [Theory]
    [InlineData("6-3")]
    [InlineData("0")]
    [InlineData("66")]
    [InlineData("x")]
    public async Task An_error_takes_one_fielder_1_to_9(string fielder)
    {
        var g = await NewGameAsync();

        Assert.Equal(BattingGridService.ErrorFielderMessage,
            await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "E", 0, fielder));

        Assert.Empty(await _grid.ListNotesAsync(g.Id));
    }

    [Theory]
    [InlineData("GO", "XP", BattingGridService.MultiOutMessage)]
    [InlineData("GO", "dp", BattingGridService.MultiOutMessage)]
    [InlineData("E", "DP", BattingGridService.ErrorMultiOutMessage)]
    [InlineData("1B", "DP", BattingGridService.NoFieldersMessage)]
    public async Task Double_or_triple_play_is_refused_where_it_does_not_apply(string code, string multiOut, string message)
    {
        var g = await NewGameAsync();

        Assert.Equal(message, await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, code, 0, "6", multiOut));

        Assert.Empty(await _grid.ListNotesAsync(g.Id));
    }

    [Theory]
    [InlineData("1B")]
    [InlineData("K")]
    [InlineData("6-3")]
    public async Task Results_without_fielders_refuse_fielders(string code)
    {
        var g = await NewGameAsync();

        Assert.Equal(BattingGridService.NoFieldersMessage,
            await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, code, 0, "8"));

        Assert.Empty(await _grid.ListNotesAsync(g.Id));
    }

    [Fact]
    public async Task Saving_a_result_without_fielders_or_clearing_removes_saved_fielders()
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "GO", 0, "4-6-3", "DP"));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "1B", 1));
        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("1B", "", ""), (note.Note, note.Fielders, note.MultiOut));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "FO", 0, "8", "DP"));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, null));
        Assert.Empty(await _grid.ListNotesAsync(g.Id));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "GO"));
        note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("", ""), (note.Fielders, note.MultiOut));
    }

    [Theory]
    [InlineData("GO", "4-6-3", "DP", "GO 4-6-3 DP")]
    [InlineData("FO", "8", "", "FO 8")]
    [InlineData("GO", "", "TP", "GO TP")]
    [InlineData("E", "6", "", "E6")]
    [InlineData("E", "", "", "E")]
    [InlineData("1B", "", "", "1B")]
    [InlineData("6-3", "", "", "6-3")]
    public void Cell_text_puts_fielders_and_double_or_triple_play_after_the_result(string code, string fielders,
        string multiOut, string expected)
    {
        var note = new BattingNote { Note = code, Fielders = fielders, MultiOut = multiOut };

        Assert.Equal(expected, AtBatResult.CellText(note));
    }

    [Theory]
    [InlineData(1, 2, "26", "2-6")]
    [InlineData(2, 3, "2-5", "2-5")]
    [InlineData(3, 4, " 12 ", "1-2")]
    [InlineData(1, 2, "2", "2")]
    public async Task Caught_stealing_saves_the_target_base_and_hyphenated_fielders_and_reloads(int bases, int target,
        string typed, string saved)
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "1B", bases,
            caughtStealingBase: target, caughtStealingFieldersText: typed));

        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("1B", bases, target, saved), (note.Note, note.BasesReached, note.CaughtStealingBase, note.CaughtStealingFielders));
    }

    [Fact]
    public async Task Caught_stealing_defaults_to_none_and_its_fielders_are_dropped_without_a_base()
    {
        var g = await NewGameAsync();

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "1B", 1));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 2, 1, "1B", 1, caughtStealingFieldersText: "26"));

        Assert.All(await _grid.ListNotesAsync(g.Id), n => Assert.Equal((0, ""), (n.CaughtStealingBase, n.CaughtStealingFielders)));
    }

    [Theory]
    [InlineData("K", 0, 2, "26", BattingGridService.CaughtStealingNotOnBaseMessage)]
    [InlineData("HR", 4, 2, "26", BattingGridService.CaughtStealingScoredMessage)]
    [InlineData("1B", 1, 3, "26", BattingGridService.CaughtStealingBaseMessage)]
    [InlineData("2B", 2, 2, "26", BattingGridService.CaughtStealingBaseMessage)]
    [InlineData("1B", 1, 1, "26", BattingGridService.CaughtStealingBaseMessage)]
    [InlineData("1B", 1, 5, "26", BattingGridService.CaughtStealingBaseMessage)]
    [InlineData("1B", 1, 2, "", BattingGridService.CaughtStealingFieldersMessage)]
    [InlineData("1B", 1, 2, "20", BattingGridService.CaughtStealingFieldersMessage)]
    [InlineData("1B", 1, 2, "2/6", BattingGridService.CaughtStealingFieldersMessage)]
    [InlineData("1B", 1, 2, "C-SS", BattingGridService.CaughtStealingFieldersMessage)]
    [InlineData("1B", 1, 2, "12345678912", BattingGridService.CaughtStealingFieldersMessage)]
    public async Task Caught_stealing_that_does_not_fit_the_at_bat_is_refused_and_changes_nothing(string code, int bases,
        int target, string fielders, string message)
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, "BB", 1));

        Assert.Equal(message, await _grid.SaveNoteAsync(g.Id, g.AwayRosterId, 1, 1, code, bases,
            caughtStealingBase: target, caughtStealingFieldersText: fielders));

        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal(("BB", 1, 0, ""), (note.Note, note.BasesReached, note.CaughtStealingBase, note.CaughtStealingFielders));
    }

    [Fact]
    public async Task Saving_without_caught_stealing_or_clearing_removes_it()
    {
        var g = await NewGameAsync();
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "1B", 1, caughtStealingBase: 2, caughtStealingFieldersText: "26"));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "1B", 1));
        var note = Assert.Single(await _grid.ListNotesAsync(g.Id));
        Assert.Equal((0, ""), (note.CaughtStealingBase, note.CaughtStealingFielders));

        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, "1B", 1, caughtStealingBase: 2, caughtStealingFieldersText: "26"));
        Assert.Null(await _grid.SaveNoteAsync(g.Id, g.HomeRosterId, 1, 1, null));
        Assert.Empty(await _grid.ListNotesAsync(g.Id));
    }

    [Theory]
    [InlineData(2, "2-6", "CS 2-6")]
    [InlineData(4, "1-2", "CS 1-2")]
    [InlineData(0, "", "")]
    public void Caught_stealing_text_shows_CS_and_the_hyphenated_fielders(int target, string fielders, string expected)
    {
        var note = new BattingNote { Note = "1B", BasesReached = 1, CaughtStealingBase = target, CaughtStealingFielders = fielders };

        Assert.Equal(expected, AtBatResult.CaughtStealingText(note));
        Assert.Equal("1B", AtBatResult.CellText(note));
    }
}
