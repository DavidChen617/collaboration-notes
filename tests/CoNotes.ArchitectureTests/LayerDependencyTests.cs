using System.Reflection;

namespace ArchitectureTests;

public class LayerDependencyTests
{
    private const string Domain = "CoNotes.Domain";
    private const string Infrastructure = "CoNotes.Infrastructure";
    private const string Api = "CoNotes.Api";

    [Fact]
    public void GivenDomainAssembly_WhenInspectingReferences_ThenItDoesNotReferenceInfrastructureOrApi()
    {
        AssertNoDependency(Domain, Infrastructure);
        AssertNoDependency(Domain, Api);
    }

    private static void AssertNoDependency(string assemblyName, string forbiddenDependencyName)
    {
        var referencedAssemblyNames = Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        Assert.DoesNotContain(forbiddenDependencyName, referencedAssemblyNames);
    }
}
