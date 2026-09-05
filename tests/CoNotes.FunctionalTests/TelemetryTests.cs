using System.Diagnostics;
using CoNotes.FunctionalTests;

namespace FunctionalTests;

[Collection(nameof(ApiCollection))]
public sealed class TelemetryTests(FunctionalTestWebAppFactory factory)
{
    [Fact]
    public async Task GivenAspNetCoreInstrumentation_WhenCallingHealthCheckEndpoint_ThenAnActivityIsStarted()
    {
        var startedActivityNames = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => startedActivityNames.Add(activity.OperationName),
        };
        ActivitySource.AddActivityListener(listener);

        var client = factory.CreateClient();
        await client.GetAsync("/health");

        Assert.Contains(startedActivityNames, name => name.Contains("Microsoft.AspNetCore.Hosting.HttpRequestIn"));
    }
}
