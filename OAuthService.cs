using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PiGUI;

internal sealed class OAuthService
{
    public async Task ConnectAsync(string provider, Action<JsonElement> onEvent, CancellationToken cancellationToken = default)
    {
        var helper = FindHelper();
        if (!File.Exists(helper)) throw new FileNotFoundException("OAuth helper is missing from the application folder.", helper);
        var psi = new ProcessStartInfo
        {
            FileName = RuntimeLocator.FindNodeExecutable(), WorkingDirectory = Path.GetDirectoryName(helper)!, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true
        };
        psi.ArgumentList.Add(helper); psi.ArgumentList.Add(provider);
        using var process = new Process { StartInfo = psi };
        process.Start();
        while (!process.HasExited)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is null) break;
            using var json = JsonDocument.Parse(line);
            onEvent(json.RootElement.Clone());
            if (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(error)) throw new InvalidOperationException(error.Trim());
        }
    }

    private static string FindHelper()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "oauth-helper.mjs"),
            Path.Combine(AppContext.BaseDirectory, "oauth-helper.mjs"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "oauth-helper.mjs"))
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public static bool IsConnected(string provider)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi", "agent", "auth.json");
        try { using var doc = JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.TryGetProperty(provider, out _); }
        catch { return false; }
    }
}
