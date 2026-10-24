using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using BaseballScorecard.Tests.Data;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Rosters;

// The roster form end to end against SCORECARD_TEST_DB: create, edit and the duplicate message.
[Collection(PostgresCollection.Name)]
public class RosterEditFormDbTests : BunitContext
{
    private readonly RosterService _service;

    public RosterEditFormDbTests(PostgresFixture fixture)
    {
        Services.AddSingleton<IDbContextFactory<ScorecardDbContext>>(new FixtureDbFactory(fixture));
        Services.AddScoped<RosterService>();
        _service = new RosterService(new FixtureDbFactory(fixture));
    }

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    public async Task Creating_through_the_form_saves_the_roster_and_returns_to_the_list()
    {
        var name = UniqueName("Cubs");
        var cut = Render<RosterEdit>();

        cut.Find("#team-name").Change(name);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/rosters", CurrentUri));
        var saved = Assert.Single(await _service.ListAsync(), r => r.TeamName == name);
        Assert.Equal(DateTime.Today.Year, saved.Year);
    }

    [Fact]
    public async Task Editing_through_the_form_loads_the_roster_and_saves_the_change()
    {
        var roster = new Roster { TeamName = UniqueName("Cubs"), Year = 2025 };
        Assert.Null(await _service.SaveAsync(roster));
        var renamed = UniqueName("Bears");

        var cut = Render<RosterEdit>(p => p.Add(x => x.Id, roster.Id));
        cut.WaitForAssertion(() => Assert.Equal(roster.TeamName, cut.Find("#team-name").GetAttribute("value")));
        Assert.Equal("2025", cut.Find("#year").GetAttribute("value"));

        cut.Find("#team-name").Change(renamed);
        cut.Find("#year").Change("2024");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/rosters", CurrentUri));
        var saved = await _service.FindAsync(roster.Id);
        Assert.Equal(renamed, saved!.TeamName);
        Assert.Equal(2024, saved.Year);
    }

    [Fact]
    public async Task Submitting_an_existing_team_name_and_year_shows_the_duplicate_message_and_saves_nothing()
    {
        var name = UniqueName("Cubs");
        Assert.Null(await _service.SaveAsync(new Roster { TeamName = name, Year = 2026 }));
        var startUri = CurrentUri;

        var cut = Render<RosterEdit>();
        cut.Find("#team-name").Change(name);
        cut.Find("#year").Change("2026");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Equal(RosterService.DuplicateMessage, cut.Find("[role=alert]").TextContent));
        Assert.Equal(startUri, CurrentUri);
        Assert.Single(await _service.ListAsync(), r => r.TeamName == name);
    }

    [Fact]
    public void Edit_page_for_a_missing_id_says_the_roster_was_not_found()
    {
        var cut = Render<RosterEdit>(p => p.Add(x => x.Id, int.MaxValue));

        cut.WaitForAssertion(() => Assert.Contains("Roster not found.", cut.Markup));
        Assert.Empty(cut.FindAll("form"));
    }
}
