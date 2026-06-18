namespace PiGUI;

internal static class ProjectWorkspace
{
    public static string DefaultRoot
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents)) documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(documents, "Pi Projects");
        }
    }

    public static string Create(string name, string parentDirectory)
    {
        var cleaned = string.Join("-", name.Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned is "." or "..") throw new ArgumentException("Enter a valid project name.");

        var root = Path.GetFullPath(parentDirectory);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, cleaned);
        var suffix = 2;
        while (Directory.Exists(path)) path = Path.Combine(root, $"{cleaned}-{suffix++}");
        Directory.CreateDirectory(path);
        return path;
    }
}
