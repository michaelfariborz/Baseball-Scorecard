using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BaseballScorecard.Tests;

public class FaviconTests
{
    private static WebApplicationFactory<Program> NewFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Scorecard"] = "Host=example;Database=favicon_tests",
                    ["Database:MigrateOnStartup"] = "false",
                }));
        });

    [Fact]
    public async Task Favicon_ico_is_served_as_an_icon()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/favicon.ico");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("icon", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Head_links_a_local_svg_icon_that_is_served()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");
        var match = System.Text.RegularExpressions.Regex.Match(
            html, "<link[^>]*rel=\"icon\"[^>]*type=\"image/svg\\+xml\"[^>]*href=\"([^\"]+)\"");

        Assert.True(match.Success, "no <link rel=\"icon\" type=\"image/svg+xml\"> in <head>");
        var href = match.Groups[1].Value;
        Assert.StartsWith("/", href);
        var svg = await client.GetAsync(href);
        Assert.Equal(System.Net.HttpStatusCode.OK, svg.StatusCode);
        Assert.Equal("image/svg+xml", svg.Content.Headers.ContentType?.MediaType);
        Assert.Contains("stroke", await svg.Content.ReadAsStringAsync());
    }
}
