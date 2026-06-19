namespace PiGUI;

internal static class ChangeTrackerSelfTest
{
    public static async Task<int> RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "PiGUI-change-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await Git(root, "init");
            File.WriteAllText(Path.Combine(root, "tracked.txt"), "committed\n");
            await Git(root, "add", "tracked.txt");
            await Git(root, "-c", "user.name=PiGUI Test", "-c", "user.email=pigui@example.invalid", "commit", "-m", "initial");
            File.WriteAllText(Path.Combine(root, "tracked.txt"), "user edit\n");
            var tracker = await WorkspaceChangeTracker.StartAsync(root) ?? throw new InvalidOperationException("Could not start the change tracker.");
            File.WriteAllText(Path.Combine(root, "tracked.txt"), "agent edit\n");
            File.WriteAllText(Path.Combine(root, "created.txt"), "new file\n");
            var changes = await tracker.FinishAsync() ?? throw new InvalidOperationException("No changes were detected.");
            if (changes.Files.Count != 2) throw new InvalidOperationException($"Expected two files, found {changes.Files.Count}.");
            await changes.UndoAsync();
            if (File.ReadAllText(Path.Combine(root, "tracked.txt")).Trim() != "user edit") throw new InvalidOperationException("Undo did not restore the pre-turn dirty file.");
            if (File.Exists(Path.Combine(root, "created.txt"))) throw new InvalidOperationException("Undo did not remove the file created by the turn.");
            Console.WriteLine("Per-turn change tracking and undo smoke test passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static async Task Git(string root, params string[] arguments)
    {
        var result = await TurnChanges.GitAsync(root, arguments);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.Error);
    }
}
