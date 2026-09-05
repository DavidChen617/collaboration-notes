using CoNotes.FunctionalTests;

namespace FunctionalTests;

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<FunctionalTestWebAppFactory>;
