namespace PiGUI;

internal static class RuntimeSelfTest
{
    public static async Task<int> RunAsync()
    {
        try
        {
            _ = RuntimeLocator.FindNodeExecutable();
            await using var client = new PiRpcClient();
            var project = Directory.GetCurrentDirectory();
            using var pollingCancellation = new CancellationTokenSource();
            var polling = Task.Run(async () =>
            {
                while (!pollingCancellation.IsCancellationRequested)
                {
                    _ = client.IsRunning;
                    await Task.Delay(5);
                }
            });
            await Task.WhenAll(
                client.StartAsync(project, "openai-codex", "gpt-5.5", "medium", "full"),
                client.StartAsync(project, "openai-codex", "gpt-5.4", "low", "full"),
                client.StartAsync(project, "openai-codex", "gpt-5.4-mini", "high", "full")
            );
            pollingCancellation.Cancel();
            await polling;
            await client.StopAsync();
            return 0;
        }
        catch { return 1; }
    }
}
