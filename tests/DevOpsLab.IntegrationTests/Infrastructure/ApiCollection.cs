using Xunit;

namespace DevOpsLab.IntegrationTests.Infrastructure;

/// <summary>
/// Every test class marked [Collection(ApiCollection.Name)] shares ONE ApiFactory,
/// and therefore one SQL Server container, for the whole test run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}