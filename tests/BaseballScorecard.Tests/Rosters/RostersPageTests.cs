using RostersPage = BaseballScorecard.Components.Pages.Rosters;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Rosters;

[Collection(PostgresCollection.Name)]
public class RostersPageTests : BunitContext
{
    public RostersPageTests(PostgresFixture fixture)
    {
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<RosterService>();
    }

    [Fact]
    public async Task Lists_each_roster_as_year_dot_team_name_linking_to_its_edit_page()
    {
        var name = $"Cubs {Guid.NewGuid():N}";
        var roster = new Roster { TeamName = name, Year = 2026 };
        Assert.Null(await Services.GetRequiredService<RosterService>().SaveAsync(roster));

        var cut = Render<RostersPage>();

        var link = cut.WaitForElement($"a[href='rosters/{roster.Id}/edit']");
        Assert.Equal($"2026 · {name}", link.TextContent);
    }
}
