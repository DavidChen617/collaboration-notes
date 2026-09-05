namespace IntegrationTests;

[CollectionDefinition(nameof(DatabaseCollection))]
public sealed class DatabaseCollection : ICollectionFixture<IntegrationTestWebAppFactory>;
