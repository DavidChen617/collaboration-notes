using System.Reflection;

namespace ArchitectureTests;

public class LayerDependencyTests
{
    private const string Domain = "Todo.Domain";
    private const string Application = "Todo.Application";
    private const string Infrastructure = "Todo.Infrastructure";
    private const string Api = "Todo.Api";

    [Fact]
    public void Domain_should_not_depend_on_application() => AssertNoDependency(Domain, Application);

    [Fact]
    public void Domain_should_not_depend_on_infrastructure() => AssertNoDependency(Domain, Infrastructure);

    [Fact]
    public void Domain_should_not_depend_on_api() => AssertNoDependency(Domain, Api);

    [Fact]
    public void Application_should_not_depend_on_infrastructure() => AssertNoDependency(Application, Infrastructure);

    [Fact]
    public void Application_should_not_depend_on_api() => AssertNoDependency(Application, Api);

    [Fact]
    public void Infrastructure_should_not_depend_on_api() => AssertNoDependency(Infrastructure, Api);

    private static void AssertNoDependency(string assemblyName, string forbiddenDependencyName)
    {
        var referencedAssemblyNames = Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        Assert.DoesNotContain(forbiddenDependencyName, referencedAssemblyNames);
    }
}

