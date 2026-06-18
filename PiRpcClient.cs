using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace PiGUI;

internal sealed class PiRpcClient : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pending = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private Process? process;
    private StreamWriter? input;
    private int nextId;

    public event Action<JsonElement>? EventReceived;
    public event Action<string>? ErrorReceived;
    public event Action? Exited;
    public bool IsRunning
    {
        get
        {
            var current = process;
            if (current is null) return false;
            try { return !current.HasExited; }
            catch (InvalidOperationException) { return false; }
        }
    }

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

    public async Task StartAsync(string projectPath, string provider, string model, string effort, string approvalMode)
    {
        await lifecycleLock.WaitAsync();
        try
        {
            await StopCoreAsync();
            if (!Directory.Exists(projectPath)) throw new DirectoryNotFoundException($"The selected workspace no longer exists: {projectPath}");
            if (!File.Exists(RuntimeCliPath)) throw new FileNotFoundException("The pi runtime is missing. Run setup.ps1 once before starting Pi GUI.", RuntimeCliPath);

            var psi = new ProcessStartInfo
            {
                FileName = RuntimeLocator.FindNodeExecutable(), WorkingDirectory = projectPath, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            psi.ArgumentList.Add(RuntimeCliPath);
        psi.ArgumentList.Add("--mode");
        psi.ArgumentList.Add("rpc");
        psi.ArgumentList.Add("--provider");
        psi.ArgumentList.Add(provider);
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model);
        psi.ArgumentList.Add("--approve");
            var approvalExtension = FindApprovalExtension();
            if (File.Exists(approvalExtension))
            {
                psi.ArgumentList.Add("--extension"); psi.ArgumentList.Add(approvalExtension); psi.Environment["PI_GUI_APPROVAL_MODE"] = approvalMode;
            }

            var owner = new Process { StartInfo = psi, EnableRaisingEvents = true };
            owner.Exited += (_, _) => { if (ReferenceEquals(process, owner)) Exited?.Invoke(); };
            try
            {
                if (!owner.Start()) throw new InvalidOperationException("Could not start the pi runtime.");
            }
            catch { owner.Dispose(); throw; }
            process = owner; input = owner.StandardInput;
            _ = ReadOutputAsync(owner); _ = ReadErrorsAsync(owner);

            await SendAsync(new { type = "get_state" }, TimeSpan.FromSeconds(25));
            await SendAsync(new { type = "set_thinking_level", level = effort });
        }
        finally { lifecycleLock.Release(); }
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

    public async Task SendRawAsync(object message)
    {
        if (!IsRunning || input is null) return;
        await writeLock.WaitAsync();
        try
        {
            await input.WriteLineAsync(JsonSerializer.Serialize(message));
            await input.FlushAsync();
        }
        finally { writeLock.Release(); }
    }

    private static string FindApprovalExtension()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "pi-gui-approval-extension.ts"),
            Path.Combine(AppContext.BaseDirectory, "pi-gui-approval-extension.ts"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pi-gui-approval-extension.ts"))
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
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
        try
        {
            while (true)
            {
                var line = await owner.StandardError.ReadLineAsync();
                if (line is null) break;
                ErrorReceived?.Invoke(line);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or IOException) { }
    }

    public async Task StopAsync()
    {
        await lifecycleLock.WaitAsync();
        try { await StopCoreAsync(); }
        finally { lifecycleLock.Release(); }
    }

    private async Task StopCoreAsync()
    {
        var owner = process;
        process = null;
        input = null;
        if (owner is null) return;
        try
        {
            bool running;
            try { running = !owner.HasExited; } catch { running = false; }
            if (running)
            {
                try { owner.Kill(true); } catch { }
                try { await owner.WaitForExitAsync(); } catch { }
            }
        }
        catch { }
        finally
        {
            owner.Dispose();
            foreach (var item in pending.Values) item.TrySetCanceled();
            pending.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        writeLock.Dispose();
        lifecycleLock.Dispose();
    }
}
