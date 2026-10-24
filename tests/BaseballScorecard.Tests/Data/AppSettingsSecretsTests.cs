using System.Text.Json;

namespace BaseballScorecard.Tests.Data;

/// <summary>
/// Connection strings live in user-secrets or env vars, never in committed appsettings files.
/// </summary>
public class AppSettingsSecretsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BaseballScorecard.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repo root (BaseballScorecard.slnx) not found.");
    }

    public static IEnumerable<object[]> AppSettingsFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Select(path => new object[] { Path.GetRelativePath(RepoRoot(), path) });

    [Fact]
    public void Appsettings_files_are_found()
    {
        Assert.NotEmpty(AppSettingsFiles());
    }

    [Theory]
    [MemberData(nameof(AppSettingsFiles))]
    public void Appsettings_file_has_no_connection_strings(string relativePath)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), relativePath)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name);

        Assert.DoesNotContain(keys, k => k.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase));
    }
}
