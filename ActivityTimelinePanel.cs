namespace PiGUI;

internal sealed class ActivityTimelinePanel : Panel
{
    private sealed record ActivityEntry(string Id, Label Label);

    private readonly ModernButton header = new() { Dock = DockStyle.Top, Height = 30, Radius = 8, Tag = "surface", Text = "Thinking…" };
    private readonly SmoothFlowLayoutPanel details = new()
    {
        AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
        Location = new Point(0, 34), Tag = "transparent-muted", Visible = false
    };
    private readonly List<ActivityEntry> entries = new();
    private bool complete;
    private bool expanded;
    private bool hasThinking;

    public event Action? TimelineHeightChanged;

    public ActivityTimelinePanel()
    {
        Height = 30; Tag = "transparent-muted";
        Controls.Add(details); Controls.Add(header);
        header.Click += (_, _) => ToggleExpanded();
        Resize += (_, _) => ResizeChildren();
    }

    public void ShowThinking()
    {
        header.Text = "Thinking…";
        if (hasThinking) return;
        hasThinking = true;
        AddEntry("thinking", "◌  Thinking through the request");
    }

    public void StartTool(string id, string tool, string detail)
    {
        var action = FriendlyAction(tool);
        var suffix = string.IsNullOrWhiteSpace(detail) ? "" : $" — {detail}";
        header.Text = $"{action}…";
        AddEntry(id, $"◌  {action}{suffix}");
    }

    public void FinishTool(string id, string tool, bool failed, string detail)
    {
        var entry = entries.LastOrDefault(item => item.Id == id);
        var action = FriendlyAction(tool);
        var suffix = string.IsNullOrWhiteSpace(detail) ? "" : $" — {detail}";
        var text = $"{(failed ? "×" : "✓")}  {action}{(failed ? " failed" : "")}{suffix}";
        if (entry is null) AddEntry(id, text);
        else
        {
            entry.Label.Text = text;
            entry.Label.ForeColor = failed ? Color.FromArgb(225, 92, 92) : Theme.Muted;
        }
        header.Text = failed ? $"{action} failed" : $"{action} finished";
    }

    public void Complete()
    {
        complete = true;
        if (entries.Count == 0)
        {
            Visible = false; Height = 0; TimelineHeightChanged?.Invoke(); return;
        }
        expanded = false;
        details.Visible = false;
        header.Text = entries.Count == 0 ? "Response complete" : $"▸  {entries.Count} {(entries.Count == 1 ? "activity" : "activities")} · View details";
        UpdateHeight();
    }

    private void AddEntry(string id, string text)
    {
        var label = new Label
        {
            Text = text, AutoEllipsis = true, Height = 24, Margin = new Padding(7, 0, 4, 1),
            Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Color.Transparent, Tag = "transparent-muted"
        };
        entries.Add(new ActivityEntry(id, label));
        details.Controls.Add(label);
        ResizeChildren();
    }

    private void ToggleExpanded()
    {
        if (!complete || entries.Count == 0) return;
        expanded = !expanded;
        details.Visible = expanded;
        header.Text = expanded
            ? $"▾  {entries.Count} {(entries.Count == 1 ? "activity" : "activities")} · Hide details"
            : $"▸  {entries.Count} {(entries.Count == 1 ? "activity" : "activities")} · View details";
        UpdateHeight();
    }

    private void UpdateHeight()
    {
        Height = expanded ? Math.Min(230, 38 + entries.Count * 25) : 30;
        ResizeChildren();
        TimelineHeightChanged?.Invoke();
    }

    private void ResizeChildren()
    {
        details.Size = new Size(Width, Math.Max(0, Height - 34));
        foreach (Control control in details.Controls) control.Width = Math.Max(100, details.ClientSize.Width - 16);
    }

    private static string FriendlyAction(string tool) => tool.ToLowerInvariant() switch
    {
        "read" => "Reading files", "bash" => "Running command", "write" => "Writing file",
        "edit" => "Editing file", "grep" => "Searching files", "find" => "Finding files", "ls" => "Listing files",
        _ => tool
    };
}
