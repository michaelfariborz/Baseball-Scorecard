using BaseballScorecard.Components.Pages;
using BaseballScorecard.Data;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BaseballScorecard.Tests.Rosters;

public class RosterEditPageTests : BunitContext
{
    public RosterEditPageTests()
    {
        // A blank Team Name and a new roster never reach the database.
        Services.AddScoped(_ => new RosterService(null!));
        Services.AddScoped(_ => new PlayerService(null!));
    }

    [Fact]
    public void New_roster_year_defaults_to_the_current_year()
    {
        var cut = Render<RosterEdit>();

        Assert.Equal(DateTime.Today.Year.ToString(), cut.Find("#year").GetAttribute("value"));
    }

    [Fact]
    public void Submitting_a_blank_team_name_shows_a_validation_message()
    {
        var cut = Render<RosterEdit>();

        cut.Find("#team-name").Change("   ");
        cut.Find("form").Submit();

        Assert.Contains(RosterService.BlankNameMessage, cut.Find(".validation-message").TextContent);
    }
}
