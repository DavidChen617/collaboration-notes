using System.Text;
using DotNet.Testcontainers.Containers;

namespace CoNotes.FunctionalTests;

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

    // Test host 是以 test assembly 輸出目錄作為 working directory 執行,
    // 而它距離 repo root 的深度因專案而異; 往上找到 .slnx 可以避免寫死
    // ".." 的層數, 不會因為改路徑就壞掉。
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (no .slnx found).");
    }
}
