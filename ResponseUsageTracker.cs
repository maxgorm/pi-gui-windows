using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace PiGUI;

internal sealed class ResponseUsageTracker
{
    private static readonly Dictionary<string, double> CopilotPaidCreditMultipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-5-mini"] = 0,
        ["gpt-5.3-codex"] = 1,
        ["gpt-5.4"] = 1,
        ["gpt-5.4-mini"] = 0.33,
        ["gpt-5.4-nano"] = 0.25,
        ["gpt-5.5"] = 7.5,
        ["claude-haiku-4.5"] = 0.33,
        ["claude-opus-4.8"] = 15,
        ["claude-fable-5"] = 1,
        ["claude-sonnet-4.6"] = 1,
        ["gemini-3.1-pro-preview"] = 1,
        ["gemini-3.5-flash"] = 14
    };

    private readonly Func<string, string> modelName;
    private long startedTimestamp;
    private string requestedModel = "";
    private string requestedProvider = "";
    private readonly List<string> actualModels = new();

    public long TotalTokens { get; private set; }
    public double EstimatedCost { get; private set; }
    public double EstimatedCopilotCredits { get; private set; }

    public ResponseUsageTracker(Func<string, string> modelName) => this.modelName = modelName;

    public void Begin(string provider, string model)
    {
        requestedProvider = provider;
        requestedModel = model;
        actualModels.Clear();
        TotalTokens = 0;
        EstimatedCost = 0;
        EstimatedCopilotCredits = 0;
        startedTimestamp = Stopwatch.GetTimestamp();
    }

    public void AddMessage(JsonElement message)
    {
        if (!message.TryGetProperty("role", out var role) || role.GetString() != "assistant") return;
        var provider = ReadString(message, "provider") ?? requestedProvider;
        var model = ReadString(message, "responseModel") ?? ReadString(message, "model") ?? requestedModel;
        if (!actualModels.Contains(model, StringComparer.OrdinalIgnoreCase)) actualModels.Add(model);

        if (message.TryGetProperty("usage", out var usage))
        {
            TotalTokens += ReadInt64(usage, "totalTokens");
            if (usage.TryGetProperty("cost", out var cost)) EstimatedCost += ReadDouble(cost, "total");
        }

        if (provider == "github-copilot")
            EstimatedCopilotCredits += CopilotPaidCreditMultipliers.GetValueOrDefault(model, 1);
    }

    public string Finish()
    {
        var elapsed = startedTimestamp == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(startedTimestamp);
        var actual = actualModels.Count == 0 ? requestedModel : string.Join(" + ", actualModels.Select(modelName));
        var requested = modelName(requestedModel);
        var identity = actualModels.Count == 1 && !string.Equals(actualModels[0], requestedModel, StringComparison.OrdinalIgnoreCase)
            ? $"Actual: {actual}  ·  requested {requested}"
            : actual;
        var duration = elapsed.TotalSeconds < 60
            ? $"{elapsed.TotalSeconds:0.0}s"
            : $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";

        if (requestedProvider == "github-copilot")
            return $"{identity}  ·  {duration}  ·  {FormatNumber(TotalTokens)} tokens  ·  ~{EstimatedCopilotCredits:0.##} premium credits";

        var cost = EstimatedCost > 0 ? $"  ·  est. ${EstimatedCost:0.####}" : "";
        return $"{identity}  ·  {duration}  ·  {FormatNumber(TotalTokens)} tokens{cost}";
    }

    public static string FormatNumber(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long ReadInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : 0;

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : 0;
}
