using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Tests.Players;

// Integration tests against SCORECARD_TEST_DB; each test makes its own roster so runs never collide.
[Collection(PostgresCollection.Name)]
public class PlayerServiceTests(PostgresFixture fixture)
{
    private readonly PlayerService _service = new(new FixtureDbFactory(fixture));

    private async Task<int> NewRosterAsync()
    {
        var roster = new Roster { TeamName = $"Cubs {Guid.NewGuid():N}", Year = 2026 };
        Assert.Null(await new RosterService(new FixtureDbFactory(fixture)).SaveAsync(roster));
        return roster.Id;
    }

    private static Player NewPlayer(int rosterId, string number, string first = "Ernie", string last = "Banks") =>
        new() { RosterId = rosterId, UniformNumber = number, FirstName = first, LastName = last };

    private async Task<List<string>> ListedAsync(int rosterId) =>
        (await _service.ListAsync(rosterId)).Select(p => $"#{p.UniformNumber} {p.FirstName} {p.LastName}").ToList();

    [Fact]
    public async Task Add_saves_the_player_to_that_roster()
    {
        var rosterId = await NewRosterAsync();
        var player = NewPlayer(rosterId, "14");

        var error = await _service.SaveAsync(player);

        Assert.Null(error);
        Assert.NotEqual(0, player.Id);
        Assert.Equal(["#14 Ernie Banks"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task Add_trims_the_fields()
    {
        var rosterId = await NewRosterAsync();

        Assert.Null(await _service.SaveAsync(NewPlayer(rosterId, " 7 ", "  Ryne ", " Sandberg  ")));

        Assert.Equal(["#7 Ryne Sandberg"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task List_sorts_by_number_value_then_by_text()
    {
        var rosterId = await NewRosterAsync();
        foreach (var number in new[] { "23", "00", "7", "10", "0", "1" })
        {
            Assert.Null(await _service.SaveAsync(NewPlayer(rosterId, number)));
        }

        var numbers = (await _service.ListAsync(rosterId)).Select(p => p.UniformNumber).ToList();

        Assert.Equal(["0", "00", "1", "7", "10", "23"], numbers);
    }

    [Fact]
    public async Task Edit_saves_the_changed_fields()
    {
        var rosterId = await NewRosterAsync();
        var player = NewPlayer(rosterId, "14");
        Assert.Null(await _service.SaveAsync(player));

        var error = await _service.SaveAsync(new Player
        {
            Id = player.Id, RosterId = rosterId, UniformNumber = "31", FirstName = "Fergie", LastName = "Jenkins",
        });

        Assert.Null(error);
        Assert.Equal(["#31 Fergie Jenkins"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task Edit_keeping_the_same_number_is_allowed()
    {
        var rosterId = await NewRosterAsync();
        var player = NewPlayer(rosterId, "14");
        Assert.Null(await _service.SaveAsync(player));

        Assert.Null(await _service.SaveAsync(new Player
        {
            Id = player.Id, RosterId = rosterId, UniformNumber = "14", FirstName = "Ernest", LastName = "Banks",
        }));

        Assert.Equal(["#14 Ernest Banks"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task Remove_deletes_the_player()
    {
        var rosterId = await NewRosterAsync();
        var keep = NewPlayer(rosterId, "14");
        var drop = NewPlayer(rosterId, "31", "Fergie", "Jenkins");
        Assert.Null(await _service.SaveAsync(keep));
        Assert.Null(await _service.SaveAsync(drop));

        await _service.DeleteAsync(drop.Id);

        Assert.Equal(["#14 Ernie Banks"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task Editing_a_player_that_was_removed_returns_a_message_and_saves_nothing()
    {
        var rosterId = await NewRosterAsync();
        var player = NewPlayer(rosterId, "14");
        Assert.Null(await _service.SaveAsync(player));
        await _service.DeleteAsync(player.Id);

        player.FirstName = "Ernest";
        var error = await _service.SaveAsync(player);

        Assert.Equal(PlayerService.PlayerRemovedMessage, error);
        Assert.Empty(await ListedAsync(rosterId));
    }

    [Theory]
    [InlineData("", "Ernie", "Banks")]
    [InlineData("14", " ", "Banks")]
    [InlineData("14", "Ernie", "")]
    public async Task Blank_field_is_rejected_and_nothing_is_saved(string number, string first, string last)
    {
        var rosterId = await NewRosterAsync();

        var error = await _service.SaveAsync(NewPlayer(rosterId, number, first, last));

        Assert.Equal(PlayerService.BlankFieldMessage, error);
        Assert.Empty(await _service.ListAsync(rosterId));
    }

    [Theory]
    [InlineData("1a")]
    [InlineData("-1")]
    [InlineData("123")]
    [InlineData("１")]
    public async Task Number_that_is_not_one_or_two_digits_is_rejected_and_nothing_is_saved(string number)
    {
        var rosterId = await NewRosterAsync();

        var error = await _service.SaveAsync(NewPlayer(rosterId, number));

        Assert.Equal(PlayerService.NumberFormatMessage, error);
        Assert.Empty(await _service.ListAsync(rosterId));
    }

    [Fact]
    public async Task Number_already_used_on_the_same_roster_is_rejected_on_add()
    {
        var rosterId = await NewRosterAsync();
        Assert.Null(await _service.SaveAsync(NewPlayer(rosterId, "14")));

        var error = await _service.SaveAsync(NewPlayer(rosterId, "14", "Fergie", "Jenkins"));

        Assert.Equal(PlayerService.DuplicateNumberMessage, error);
        Assert.Equal(["#14 Ernie Banks"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task Number_already_used_on_the_same_roster_is_rejected_on_edit()
    {
        var rosterId = await NewRosterAsync();
        Assert.Null(await _service.SaveAsync(NewPlayer(rosterId, "14")));
        var other = NewPlayer(rosterId, "31", "Fergie", "Jenkins");
        Assert.Null(await _service.SaveAsync(other));

        var error = await _service.SaveAsync(new Player
        {
            Id = other.Id, RosterId = rosterId, UniformNumber = "14", FirstName = "Fergie", LastName = "Jenkins",
        });

        Assert.Equal(PlayerService.DuplicateNumberMessage, error);
        Assert.Equal(["#14 Ernie Banks", "#31 Fergie Jenkins"], await ListedAsync(rosterId));
    }

    [Fact]
    public async Task Zero_and_double_zero_are_different_numbers()
    {
        var rosterId = await NewRosterAsync();
        Assert.Null(await _service.SaveAsync(NewPlayer(rosterId, "0")));

        Assert.Null(await _service.SaveAsync(NewPlayer(rosterId, "00")));

        Assert.Equal(2, (await _service.ListAsync(rosterId)).Count);
    }

    [Fact]
    public async Task Same_number_on_a_different_roster_is_allowed()
    {
        var cubs = await NewRosterAsync();
        var sox = await NewRosterAsync();
        Assert.Null(await _service.SaveAsync(NewPlayer(cubs, "14")));

        var error = await _service.SaveAsync(NewPlayer(sox, "14", "Paul", "Konerko"));

        Assert.Null(error);
        Assert.Equal(["#14 Paul Konerko"], await ListedAsync(sox));
    }

    [Fact]
    public async Task Database_unique_index_rejects_a_duplicate_number_on_one_roster()
    {
        var rosterId = await NewRosterAsync();
        await using (var db = fixture.CreateContext())
        {
            db.Players.Add(NewPlayer(rosterId, "14"));
            await db.SaveChangesAsync();
        }

        await using var db2 = fixture.CreateContext();
        db2.Players.Add(NewPlayer(rosterId, "14", "Fergie", "Jenkins"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }
}
