using RostersPage = BaseballScorecard.Components.Pages.Rosters;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
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

    private async Task<Roster> SaveAsync(string name, int year)
    {
        var roster = new Roster { TeamName = name, Year = year };
        Assert.Null(await Services.GetRequiredService<RosterService>().SaveAsync(roster));
        return roster;
    }

    private static List<string> RosterLinks(IRenderedComponent<RostersPage> cut) =>
        cut.FindAll("ul.rosters a").Select(a => a.GetAttribute("href")!).ToList();

    private static List<string> YearOptions(IRenderedComponent<RostersPage> cut) =>
        cut.FindAll("#year-filter option").Select(o => o.TextContent).ToList();

    private static Task PickYear(IRenderedComponent<RostersPage> cut, string value) =>
        cut.InvokeAsync(() => cut.Find("#year-filter").ChangeAsync(new ChangeEventArgs { Value = value }));

    [Fact]
    public async Task Year_select_is_labelled_starts_on_all_and_lists_roster_years_newest_first()
    {
        await SaveAsync($"Cubs {Guid.NewGuid():N}", 2025);
        await SaveAsync($"Cubs {Guid.NewGuid():N}", 2026);

        var cut = Render<RostersPage>();

        cut.WaitForAssertion(() => Assert.Contains("2025", YearOptions(cut)));
        Assert.Equal("Year", cut.Find("label[for='year-filter']").TextContent);
        var options = YearOptions(cut);
        Assert.Equal("All", options[0]);
        Assert.True(options.IndexOf("2026") < options.IndexOf("2025"));
        Assert.DoesNotContain("1872", options);
        Assert.Equal("All", cut.Find("#year-filter option:checked").TextContent);
    }

    [Fact]
    public async Task Picking_a_year_shows_only_that_years_rosters_and_all_shows_every_roster()
    {
        var cubs25 = await SaveAsync($"Cubs {Guid.NewGuid():N}", 2025);
        var cubs26 = await SaveAsync($"Cubs {Guid.NewGuid():N}", 2026);
        var mets26 = await SaveAsync($"Mets {Guid.NewGuid():N}", 2026);
        // Another test may leave a lowercase-named 2026 roster in the shared database.
        var lower26 = await SaveAsync($"f{Guid.NewGuid():N} A", 2026);
        var cut = Render<RostersPage>();
        cut.WaitForAssertion(() => Assert.Contains("2025", YearOptions(cut)));

        await PickYear(cut, "2026");

        cut.WaitForAssertion(() => Assert.DoesNotContain($"rosters/{cubs25.Id}/edit", RosterLinks(cut)));
        Assert.Contains($"rosters/{cubs26.Id}/edit", RosterLinks(cut));
        Assert.Contains($"rosters/{mets26.Id}/edit", RosterLinks(cut));
        Assert.All(cut.FindAll("ul.rosters a"), a => Assert.StartsWith("2026 · ", a.TextContent));
        Assert.Contains($"rosters/{lower26.Id}/edit", RosterLinks(cut));
        // Check the order of this test's own rosters only; other tests' rows share the database.
        var own = new[] { cubs26.Id, mets26.Id }.Select(id => $"rosters/{id}/edit").ToList();
        var ownLinks = RosterLinks(cut).Where(own.Contains).ToList();
        Assert.Equal(own, ownLinks);

        await PickYear(cut, "");

        cut.WaitForAssertion(() => Assert.Contains($"rosters/{cubs25.Id}/edit", RosterLinks(cut)));
        Assert.Contains($"rosters/{cubs26.Id}/edit", RosterLinks(cut));
    }

    [Fact]
    public async Task With_no_rosters_at_all_the_page_says_so_and_the_select_has_only_all()
    {
        var empty = new PostgresFixture();
        await empty.InitializeAsync();
        try
        {
            using var ctx = new BunitContext();
            ctx.Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(empty));
            ctx.Services.AddScoped<RosterService>();

            var cut = ctx.Render<RostersPage>();

            cut.WaitForAssertion(() => Assert.Contains("No rosters yet.", cut.Markup));
            Assert.Equal(["All"], YearOptions(cut));
        }
        finally
        {
            await empty.DisposeAsync();
        }
    }
}
