using BuildBlock;

namespace FunctionalTests;

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<IntegrationTestWebAppFactory>;

