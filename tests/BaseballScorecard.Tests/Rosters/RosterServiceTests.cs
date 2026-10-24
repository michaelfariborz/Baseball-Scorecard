using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Tests.Rosters;

// Integration tests against SCORECARD_TEST_DB; unique team names keep runs from colliding.
[Collection(PostgresCollection.Name)]
public class RosterServiceTests(PostgresFixture fixture)
{
    private readonly RosterService _service = new(new FixtureDbFactory(fixture));

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    [Fact]
    public async Task Create_saves_the_roster_and_it_is_listed()
    {
        var name = UniqueName("Cubs");
        var roster = new Roster { TeamName = name, Year = 2026 };

        var error = await _service.SaveAsync(roster);

        Assert.Null(error);
        Assert.NotEqual(0, roster.Id);
        var listed = Assert.Single(await _service.ListAsync(), r => r.TeamName == name);
        Assert.Equal(2026, listed.Year);
    }

    [Fact]
    public async Task Edit_saves_a_new_team_name_and_year()
    {
        var roster = new Roster { TeamName = UniqueName("Cubs"), Year = 2025 };
        Assert.Null(await _service.SaveAsync(roster));
        var renamed = UniqueName("Bears");

        var error = await _service.SaveAsync(new Roster { Id = roster.Id, TeamName = renamed, Year = 2024 });

        Assert.Null(error);
        var saved = await _service.FindAsync(roster.Id);
        Assert.Equal(renamed, saved!.TeamName);
        Assert.Equal(2024, saved.Year);
    }

    [Fact]
    public async Task List_orders_newest_year_first_then_by_name()
    {
        var tag = Guid.NewGuid().ToString("N");
        foreach (var (name, year) in new[] { ("B", 2025), ("A", 2025), ("C", 2026), ("A", 2024) })
        {
            Assert.Null(await _service.SaveAsync(new Roster { TeamName = $"{tag} {name}", Year = year }));
        }

        var mine = (await _service.ListAsync())
            .Where(r => r.TeamName.StartsWith(tag))
            .Select(r => $"{r.Year} {r.TeamName[(tag.Length + 1)..]}")
            .ToList();

        Assert.Equal(["2026 C", "2025 A", "2025 B", "2024 A"], mine);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_team_name_is_rejected_and_nothing_is_saved(string name)
    {
        var before = (await _service.ListAsync()).Count;

        var error = await _service.SaveAsync(new Roster { TeamName = name, Year = 2026 });

        Assert.Equal(RosterService.BlankNameMessage, error);
        Assert.Equal(before, (await _service.ListAsync()).Count);
    }

    [Fact]
    public async Task Duplicate_team_name_and_year_is_rejected_on_create()
    {
        var name = UniqueName("Cubs");
        Assert.Null(await _service.SaveAsync(new Roster { TeamName = name, Year = 2026 }));

        var error = await _service.SaveAsync(new Roster { TeamName = name, Year = 2026 });

        Assert.Equal(RosterService.DuplicateMessage, error);
        Assert.Single(await _service.ListAsync(), r => r.TeamName == name);
    }

    [Fact]
    public async Task Same_team_name_in_another_year_is_a_separate_roster()
    {
        var name = UniqueName("Cubs");
        Assert.Null(await _service.SaveAsync(new Roster { TeamName = name, Year = 2026 }));

        Assert.Null(await _service.SaveAsync(new Roster { TeamName = name, Year = 2025 }));

        Assert.Equal(2, (await _service.ListAsync()).Count(r => r.TeamName == name));
    }

    [Fact]
    public async Task Duplicate_team_name_and_year_is_rejected_on_edit()
    {
        var name = UniqueName("Cubs");
        Assert.Null(await _service.SaveAsync(new Roster { TeamName = name, Year = 2026 }));
        var other = new Roster { TeamName = name, Year = 2025 };
        Assert.Null(await _service.SaveAsync(other));

        var error = await _service.SaveAsync(new Roster { Id = other.Id, TeamName = name, Year = 2026 });

        Assert.Equal(RosterService.DuplicateMessage, error);
        Assert.Equal(2025, (await _service.FindAsync(other.Id))!.Year);
    }

    [Fact]
    public async Task Database_unique_index_rejects_a_duplicate_pair()
    {
        var name = UniqueName("Cubs");
        await using (var db = fixture.CreateContext())
        {
            db.Rosters.Add(new Roster { TeamName = name, Year = 2026 });
            await db.SaveChangesAsync();
        }

        await using var db2 = fixture.CreateContext();
        db2.Rosters.Add(new Roster { TeamName = name, Year = 2026 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }
}
