using System.Text;
using DotNet.Testcontainers.Containers;

namespace IntegrationTests;

internal static class MigrationRunner
{
    private static readonly string MigrationsDirectory = Path.Combine(
        FindRepositoryRoot(), "src", "CoNotes.Infrastructure", "Persistence", "Migrations");

    private static readonly IOrderedEnumerable<string> UpScripts = Directory
        .GetFiles(MigrationsDirectory, "*.up.sql")
        .OrderBy(f => f, StringComparer.Ordinal);

    public static async Task RunAsync(Func<string, CancellationToken, Task<ExecResult>> execScriptAsync)
    {
        var sb = new StringBuilder();

        foreach (var script in UpScripts)
        {
            var sql = await File.ReadAllTextAsync(script);
            sb.Append(sql);
        }

        await execScriptAsync(sb.ToString(), CancellationToken.None);
    }

    // Test hosts run with the test assembly's output directory as the working
    // directory, whose depth from the repo root varies by project; walking up
    // to the .slnx avoids hard-coding a ".." count that breaks on any rename.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (no .slnx found).");
    }
}
