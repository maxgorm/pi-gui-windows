namespace PiGUI;

internal static class RuntimeLocator
{
    public static string FindNodeExecutable()
    {
        var candidates = new List<string>();
        AddEnvironmentPath(candidates, "NVM_SYMLINK");
        AddEnvironmentPath(candidates, "NVM_HOME");

        foreach (var scope in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var path = Environment.GetEnvironmentVariable("PATH", scope);
            if (string.IsNullOrWhiteSpace(path)) continue;
            candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => Path.Combine(directory.Trim('"'), "node.exe")));
        }

        candidates.AddRange(new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"),
            @"C:\nvm4w\nodejs\node.exe"
        });

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Node.js could not be found. Install Node.js 22.19 or newer, then restart Pi GUI.");
    }

    private static void AddEnvironmentPath(List<string> candidates, string variable)
    {
        foreach (var scope in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var value = Environment.GetEnvironmentVariable(variable, scope);
            if (!string.IsNullOrWhiteSpace(value)) candidates.Add(Path.Combine(value.Trim('"'), "node.exe"));
        }
    }
}
