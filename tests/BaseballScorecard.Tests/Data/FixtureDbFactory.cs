using BaseballScorecard.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Tests.Data;

/// <summary>Hands out contexts on the fixture's migrated run database, for code that takes a context factory.</summary>
public sealed class FixtureDbFactory(PostgresFixture fixture) : IDbContextFactory<ScorecardDbContext>
{
    public ScorecardDbContext CreateDbContext() => fixture.CreateContext();
}
