using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace PiGUI;

internal sealed class PiRpcClient : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pending = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private Process? process;
    private StreamWriter? input;
    private int nextId;

    public event Action<JsonElement>? EventReceived;
    public event Action<string>? ErrorReceived;
    public event Action? Exited;
    public bool IsRunning => process is { HasExited: false };

    public static string RuntimeCliPath
    {
        get
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "node_modules", "@earendil-works", "pi-coding-agent", "dist", "cli.js"),
                Path.Combine(Directory.GetCurrentDirectory(), "node_modules", "@earendil-works", "pi-coding-agent", "dist", "cli.js"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "node_modules", "@earendil-works", "pi-coding-agent", "dist", "cli.js"))
            };
            return candidates.FirstOrDefault(File.Exists) ?? candidates[1];
        }
    }

    public async Task StartAsync(string projectPath, string provider, string model, string effort)
    {
        await StopAsync();
        if (!File.Exists(RuntimeCliPath))
            throw new FileNotFoundException("The pi runtime is missing. Run setup.ps1 once before starting Pi GUI.", RuntimeCliPath);

        var psi = new ProcessStartInfo
        {
            FileName = "node.exe",
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(RuntimeCliPath);
        psi.ArgumentList.Add("--mode");
        psi.ArgumentList.Add("rpc");
        psi.ArgumentList.Add("--provider");
        psi.ArgumentList.Add(provider);
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);
        psi.ArgumentList.Add("--approve");

        process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Exited += (_, _) => Exited?.Invoke();
        if (!process.Start()) throw new InvalidOperationException("Could not start the pi runtime.");
        input = process.StandardInput;
        _ = ReadOutputAsync(process);
        _ = ReadErrorsAsync(process);

        await SendAsync(new { type = "get_state" }, TimeSpan.FromSeconds(25));
        await SendAsync(new { type = "set_thinking_level", level = effort });
    }

    public async Task<JsonElement> SendAsync(object command, TimeSpan? timeout = null)
    {
        if (!IsRunning || input is null) throw new InvalidOperationException("The pi runtime is not connected.");
        var id = $"gui-{Interlocked.Increment(ref nextId)}";
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(command));
        var map = new Dictionary<string, object?> { ["id"] = id };
        foreach (var property in document.RootElement.EnumerateObject()) map[property.Name] = property.Value.Clone();
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;

        await writeLock.WaitAsync();
        try
        {
            await input.WriteLineAsync(JsonSerializer.Serialize(map));
            await input.FlushAsync();
        }
        finally { writeLock.Release(); }

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
        using var registration = cts.Token.Register(() => completion.TrySetException(new TimeoutException("pi did not respond in time.")));
        try
        {
            var response = await completion.Task;
            if (response.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                var message = response.TryGetProperty("error", out var error) ? error.GetString() : "Unknown pi error";
                throw new InvalidOperationException(message);
            }
            return response;
        }
        finally { pending.TryRemove(id, out _); }
    }

    public Task PromptAsync(string message, IEnumerable<Attachment> attachments)
    {
        var files = attachments.ToList();
        var nonImages = files.Where(a => !a.IsImage).ToList();
        if (nonImages.Count > 0)
        {
            message += "\n\nAttached files (open these exact local paths as needed):\n" +
                string.Join("\n", nonImages.Select(a => $"- {a.Path}"));
        }
        var images = files.Where(a => a.IsImage).Select(a => new
        {
            type = "image",
            data = Convert.ToBase64String(File.ReadAllBytes(a.Path)),
            mimeType = MimeFor(a.Path)
        }).ToArray();
        return SendAsync(new { type = "prompt", message, images }, TimeSpan.FromMinutes(2));
    }

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/png"
    };

    private async Task ReadOutputAsync(Process owner)
    {
        try
        {
            while (!owner.HasExited)
            {
                var line = await owner.StandardOutput.ReadLineAsync();
                if (line is null) break;
                try
                {
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement.Clone();
                    if (root.TryGetProperty("type", out var type) && type.GetString() == "response" &&
                        root.TryGetProperty("id", out var id) && id.GetString() is { } key && pending.TryGetValue(key, out var tcs))
                        tcs.TrySetResult(root);
                    else EventReceived?.Invoke(root);
                }
                catch (JsonException) { ErrorReceived?.Invoke(line); }
            }
        }
        catch (Exception ex) { ErrorReceived?.Invoke(ex.Message); }
    }

    private async Task ReadErrorsAsync(Process owner)
    {
        while (!owner.HasExited)
        {
            var line = await owner.StandardError.ReadLineAsync();
            if (line is null) break;
            ErrorReceived?.Invoke(line);
        }
    }

    public async Task StopAsync()
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                try { await SendAsync(new { type = "abort" }, TimeSpan.FromSeconds(2)); } catch { }
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
        catch { }
        finally
        {
            process.Dispose();
            process = null;
            input = null;
            foreach (var item in pending.Values) item.TrySetCanceled();
            pending.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        writeLock.Dispose();
    }
}
