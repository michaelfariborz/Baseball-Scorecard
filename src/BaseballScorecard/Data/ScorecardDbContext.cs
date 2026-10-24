using Microsoft.EntityFrameworkCore;

namespace BaseballScorecard.Data;

/// <summary>
/// EF Core context for the scorecard's Postgres database. Tables arrive with later migrations.
/// </summary>
public class ScorecardDbContext(DbContextOptions<ScorecardDbContext> options) : DbContext(options);
