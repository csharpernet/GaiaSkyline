namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Shares a single <see cref="PublicSiteFactory"/> (one migrated + seeded LocalDB, one set of
/// generated media rasters) across every end-to-end test class. Classes in the collection run
/// serially, so they never migrate/seed the same LocalDB instance concurrently.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PublicSiteCollection : ICollectionFixture<PublicSiteFactory>
{
    public const string Name = "PublicSite";
}
