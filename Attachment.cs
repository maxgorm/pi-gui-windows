namespace PiGUI;

internal sealed record Attachment(string Path, bool IsImage)
{
    public string Name => System.IO.Path.GetFileName(Path);
}
