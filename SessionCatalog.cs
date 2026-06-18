using System.Text.Json;

namespace PiGUI;

internal sealed record SavedSession(string FilePath, string ProjectPath, string Title, DateTime UpdatedAt, string Provider, string Model, string Effort);

internal static class SessionCatalog
{
    public static IReadOnlyList<SavedSession> Load()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi", "agent", "sessions");
        if (!Directory.Exists(root)) return Array.Empty<SavedSession>();

        var sessions = new List<SavedSession>();
        foreach (var file in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
        {
            try
            {
                string? project = null;
                string? name = null;
                string? firstPrompt = null;
                var provider = "openai-codex";
                var model = "gpt-5.5";
                var effort = "medium";
                foreach (var line in File.ReadLines(file))
                {
                    using var document = JsonDocument.Parse(line);
                    var entry = document.RootElement;
                    var type = entry.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
                    if (type == "session" && entry.TryGetProperty("cwd", out var cwd)) project = cwd.GetString();
                    else if (type == "session_info" && entry.TryGetProperty("name", out var title)) name = title.GetString();
                    else if (type == "model_change")
                    {
                        if (entry.TryGetProperty("provider", out var providerNode)) provider = providerNode.GetString() ?? provider;
                        if (entry.TryGetProperty("modelId", out var modelNode)) model = modelNode.GetString() ?? model;
                    }
                    else if (type == "thinking_level_change" && entry.TryGetProperty("thinkingLevel", out var effortNode)) effort = effortNode.GetString() ?? effort;
                    else if (firstPrompt is null && type == "message" && entry.TryGetProperty("message", out var message) &&
                             message.TryGetProperty("role", out var role) && role.GetString() == "user")
                        firstPrompt = ReadText(message);
                }
                if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(firstPrompt)) continue;
                var titleText = CleanTitle(string.IsNullOrWhiteSpace(name) ? firstPrompt : name!);
                sessions.Add(new SavedSession(file, project!, titleText, File.GetLastWriteTime(file), provider, model, effort));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return sessions.OrderByDescending(session => session.UpdatedAt).ToList();
    }

    public static string ReadText(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content)) return "";
        if (content.ValueKind == JsonValueKind.String) return content.GetString() ?? "";
        if (content.ValueKind != JsonValueKind.Array) return "";
        return string.Join("\n", content.EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "text" && item.TryGetProperty("text", out _))
            .Select(item => item.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string CleanTitle(string value)
    {
        var title = string.Join(" ", value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return title.Length <= 31 ? title : title[..28] + "…";
    }
}
