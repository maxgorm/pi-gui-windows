using System.Diagnostics;
using System.Text;

namespace PiGUI;

internal sealed record ChangedFile(string Path, int Additions, int Deletions);

internal sealed class TurnChanges
{
    public required string ProjectPath { get; init; }
    public required string Patch { get; init; }
    public required IReadOnlyList<ChangedFile> Files { get; init; }
    public int Additions => Files.Sum(file => file.Additions);
    public int Deletions => Files.Sum(file => file.Deletions);
    public bool IsEmpty => string.IsNullOrWhiteSpace(Patch) || Files.Count == 0;

    public async Task UndoAsync()
    {
        if (IsEmpty) return;
        var result = await GitAsync(ProjectPath, new[] { "apply", "--reverse", "--binary", "--whitespace=nowarn", "-" }, Patch);
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? "The changes could not be undone because the workspace has changed since this turn." : result.Error.Trim());
    }

    internal static async Task<GitResult> GitAsync(string workingDirectory, IEnumerable<string> arguments, string? input = null, string? indexPath = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git", WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input is not null,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        if (indexPath is not null) psi.Environment["GIT_INDEX_FILE"] = indexPath;
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = psi };
        process.Start();
        if (input is not null) { await process.StandardInput.WriteAsync(input); process.StandardInput.Close(); }
        var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new GitResult(process.ExitCode, await outputTask, await errorTask);
    }

    internal sealed record GitResult(int ExitCode, string Output, string Error);
}

internal sealed class WorkspaceChangeTracker : IAsyncDisposable
{
    private readonly string projectPath;
    private readonly string indexPath;
    private bool finished;

    private WorkspaceChangeTracker(string projectPath, string indexPath) { this.projectPath = projectPath; this.indexPath = indexPath; }

    public static async Task<WorkspaceChangeTracker?> StartAsync(string projectPath)
    {
        var inside = await TurnChanges.GitAsync(projectPath, new[] { "rev-parse", "--is-inside-work-tree" });
        if (inside.ExitCode != 0 || !inside.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)) return null;
        var index = Path.Combine(Path.GetTempPath(), "PiGUI", "turn-snapshots", Guid.NewGuid().ToString("N") + ".index");
        Directory.CreateDirectory(Path.GetDirectoryName(index)!);
        var tracker = new WorkspaceChangeTracker(projectPath, index);
        var read = await TurnChanges.GitAsync(projectPath, new[] { "read-tree", "HEAD" }, indexPath: index);
        if (read.ExitCode != 0) { await tracker.DisposeAsync(); return null; }
        var add = await TurnChanges.GitAsync(projectPath, new[] { "add", "-A", "--", "." }, indexPath: index);
        if (add.ExitCode != 0) { await tracker.DisposeAsync(); return null; }
        return tracker;
    }

    public async Task<TurnChanges?> FinishAsync()
    {
        if (finished) return null; finished = true;
        try
        {
            await TurnChanges.GitAsync(projectPath, new[] { "add", "-N", "--", "." }, indexPath: indexPath);
            var patch = await TurnChanges.GitAsync(projectPath, new[] { "diff", "--binary", "--no-ext-diff", "--", "." }, indexPath: indexPath);
            var stat = await TurnChanges.GitAsync(projectPath, new[] { "diff", "--numstat", "--no-ext-diff", "--", "." }, indexPath: indexPath);
            if (patch.ExitCode != 0 || string.IsNullOrWhiteSpace(patch.Output)) return null;
            var files = ParseNumStat(stat.Output);
            return new TurnChanges { ProjectPath = projectPath, Patch = patch.Output, Files = files };
        }
        finally { await DisposeAsync(); }
    }

    private static IReadOnlyList<ChangedFile> ParseNumStat(string output)
    {
        var files = new List<ChangedFile>();
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t'); if (parts.Length < 3) continue;
            _ = int.TryParse(parts[0], out var additions); _ = int.TryParse(parts[1], out var deletions);
            files.Add(new ChangedFile(parts[^1], additions, deletions));
        }
        return files;
    }

    public ValueTask DisposeAsync()
    {
        try { if (File.Exists(indexPath)) File.Delete(indexPath); } catch { }
        try { var lockPath = indexPath + ".lock"; if (File.Exists(lockPath)) File.Delete(lockPath); } catch { }
        return ValueTask.CompletedTask;
    }
}
