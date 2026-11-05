using System.Text.RegularExpressions;

namespace BaseballScorecard.Tests.BattingGrids;

// The batting grid draws its cell borders from app.css (no scoped stylesheet), keyed on the table's class.
public class BattingGridBorderStyleTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "BaseballScorecard", "wwwroot", "app.css")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir.FullName, "src", "BaseballScorecard", "wwwroot", "app.css"));
    }

    private static string Rule(string selector)
    {
        var m = Regex.Match(Css(), Regex.Escape(selector) + @"\s*(?:,[^{]*)?\{([^}]*)\}");
        Assert.True(m.Success, $"no CSS rule for {selector}");
        return m.Groups[1].Value;
    }

    [Fact]
    public void Table_collapses_borders_and_draws_one()
    {
        var rule = Rule(".batting-grid");
        Assert.Contains("border-collapse: collapse", rule);
        Assert.Matches(@"border:\s*1px solid", rule);
    }

    [Fact]
    public void Header_and_body_cells_draw_a_solid_border()
    {
        Assert.Matches(@"border:\s*1px solid", Rule(".batting-grid th"));
        Assert.Matches(@"border:\s*1px solid", Rule(".batting-grid td"));
    }

    [Fact]
    public void Grid_table_carries_the_batting_grid_class()
    {
        var razor = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "BaseballScorecard", "Components", "Pages", "BattingGrid.razor"));
        Assert.Contains("<table class=\"batting-grid\">", razor);
    }
}
