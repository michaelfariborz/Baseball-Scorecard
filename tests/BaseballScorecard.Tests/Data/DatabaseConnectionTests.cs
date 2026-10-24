namespace BaseballScorecard.Tests.Data;

[Collection(PostgresCollection.Name)]
public class DatabaseConnectionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Test_database_is_reachable_after_migrations()
    {
        await using var db = fixture.CreateContext();

        Assert.True(await db.Database.CanConnectAsync());
    }
}

public class PostgresFixtureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_test_db_variable_fails_with_a_message_naming_it(string? value)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PostgresFixture.ReadConnectionString(_ => value));

        Assert.Contains("SCORECARD_TEST_DB", ex.Message);
    }

    [Fact]
    public void Set_test_db_variable_is_returned()
    {
        var value = PostgresFixture.ReadConnectionString(
            name => name == "SCORECARD_TEST_DB" ? "Host=localhost;Database=x" : null);

        Assert.Equal("Host=localhost;Database=x", value);
    }
}
