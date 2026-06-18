using System.Text.Json;

namespace PiGUI;

internal sealed class AppSettings
{
    public string ProjectPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string Provider { get; set; } = "openai-codex";
    public string Model { get; set; } = "gpt-5.5";
    public string Effort { get; set; } = "medium";
    public string ApprovalMode { get; set; } = "ask";
    public string ThemeMode { get; set; } = "dark";
    public List<string> RecentProjects { get; set; } = new();

    private static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PiGUI");
    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        RecentProjects.RemoveAll(p => !Directory.Exists(p));
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void RememberProject(string path)
    {
        ProjectPath = path;
        RecentProjects.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentProjects.Insert(0, path);
        if (RecentProjects.Count > 8) RecentProjects.RemoveRange(8, RecentProjects.Count - 8);
        Save();
    }
}
