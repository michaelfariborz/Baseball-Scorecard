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
}
