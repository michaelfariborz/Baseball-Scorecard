using System.Globalization;
using System.Text.RegularExpressions;

namespace BaseballScorecard.Tests;

// The shared look in app.css: color tokens on :root with a dark-mode set, WCAG AA contrast for every
// text pairing in both modes, shared control styles and a visible focus outline.
public class AppStyleTests
{
    private static readonly string[] Tokens =
    [
        "--color-bg", "--color-surface", "--color-text", "--color-muted", "--color-primary", "--color-on-primary",
        "--color-link", "--color-accent", "--color-border", "--color-focus", "--color-header", "--color-on-header",
    ];

    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "BaseballScorecard", "wwwroot", "app.css")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir.FullName, "src", "BaseballScorecard", "wwwroot", "app.css"));
    }

    private static Dictionary<string, string> Vars(string block) =>
        Regex.Matches(block, @"(--[a-z0-9-]+)\s*:\s*([^;]+);")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());

    private static Dictionary<string, string> Light()
    {
        var m = Regex.Match(Css(), @"(?m)^:root\s*\{([^}]*)\}");
        Assert.True(m.Success, "no top-level :root rule");
        return Vars(m.Groups[1].Value);
    }

    private static Dictionary<string, string> Dark()
    {
        var m = Regex.Match(Css(), @"@media\s*\(prefers-color-scheme:\s*dark\)\s*\{\s*:root\s*\{([^}]*)\}");
        Assert.True(m.Success, "no prefers-color-scheme: dark :root rule");
        return Vars(m.Groups[1].Value);
    }

    private static double Luminance(string hex)
    {
        hex = hex.TrimStart('#');
        double Channel(int i)
        {
            var c = int.Parse(hex.Substring(i, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static string Rule(string selector)
    {
        var m = Regex.Match(Css(), @"(?m)^[^{}@]*" + Regex.Escape(selector) + @"[^{}]*\{([^}]*)\}");
        Assert.True(m.Success, $"no CSS rule for {selector}");
        return m.Groups[1].Value;
    }

    [Fact]
    public void Light_and_dark_sets_define_every_color_token_as_a_hex_color()
    {
        foreach (var set in new[] { Light(), Dark() })
            foreach (var token in Tokens)
            {
                Assert.True(set.ContainsKey(token), $"missing {token}");
                Assert.Matches("^#[0-9A-Fa-f]{6}$", set[token]);
            }
    }

    [Fact]
    public void Root_defines_spacing_and_font_tokens()
    {
        var light = Light();
        Assert.Contains("--font-body", light.Keys);
        Assert.Contains("--space-2", light.Keys);
        Assert.Contains("--radius", light.Keys);
    }

    [Fact]
    public void Dark_set_swaps_the_page_colors()
    {
        Assert.NotEqual(Light()["--color-bg"], Dark()["--color-bg"]);
        Assert.NotEqual(Light()["--color-text"], Dark()["--color-text"]);
    }

    [Theory]
    [InlineData("--color-text", "--color-bg", 4.5)]
    [InlineData("--color-text", "--color-surface", 4.5)]
    [InlineData("--color-muted", "--color-bg", 4.5)]
    [InlineData("--color-muted", "--color-surface", 4.5)]
    [InlineData("--color-link", "--color-bg", 4.5)]
    [InlineData("--color-link", "--color-surface", 4.5)]
    [InlineData("--color-on-primary", "--color-primary", 4.5)]
    [InlineData("--color-accent", "--color-bg", 4.5)]
    [InlineData("--color-accent", "--color-surface", 4.5)]
    [InlineData("--color-on-header", "--color-header", 4.5)]
    [InlineData("--color-border", "--color-bg", 3.0)]
    [InlineData("--color-border", "--color-surface", 3.0)]
    [InlineData("--color-focus", "--color-bg", 3.0)]
    [InlineData("--color-focus", "--color-surface", 3.0)]
    public void Pairs_meet_wcag_aa_in_light_and_dark(string fg, string bg, double min)
    {
        foreach (var (name, set) in new[] { ("light", Light()), ("dark", Dark()) })
        {
            var ratio = Contrast(set[fg], set[bg]);
            Assert.True(ratio >= min, $"{name}: {fg} on {bg} is {ratio:F2}:1, want {min}:1");
        }
    }

    [Theory]
    [InlineData("body")]
    [InlineData("button")]
    [InlineData("select")]
    [InlineData("input")]
    [InlineData("table")]
    public void Controls_take_their_colors_from_the_tokens(string selector)
    {
        Assert.Contains("var(--color-", Rule(selector));
    }

    [Fact]
    public void Body_uses_the_font_token_and_an_explicit_background()
    {
        var body = Rule("body");
        Assert.Contains("var(--font-body)", body);
        Assert.Contains("background: var(--color-bg)", body);
    }

    [Fact]
    public void Every_focusable_element_shows_a_focus_outline()
    {
        var focus = Rule(":focus-visible");
        Assert.Matches(@"outline:\s*\d+px solid var\(--color-focus\)", focus);
    }

    [Fact]
    public void Header_focus_outline_uses_the_header_text_color()
    {
        Assert.Contains("var(--color-on-header)", Rule(".app-header :focus-visible"));
    }

    [Fact]
    public void Current_nav_link_is_marked_by_aria_current()
    {
        Assert.Contains("var(--color-accent)", Rule(".app-nav a[aria-current=\"page\"]"));
    }

    [Fact]
    public void At_bat_dialog_uses_the_surface_token_so_it_follows_dark_mode()
    {
        var dialog = Rule(".at-bat-dialog");
        Assert.Contains("background: var(--color-surface)", dialog);
        Assert.Contains("color: var(--color-text)", dialog);
    }
}
