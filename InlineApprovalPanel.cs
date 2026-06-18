namespace PiGUI;

internal sealed class InlineApprovalPanel : RoundedPanel
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> Decision => completion.Task;

    public InlineApprovalPanel(string title, string detail)
    {
        Width = 720; Height = 126; Margin = new Padding(4, 5, 0, 12); Padding = new Padding(14); Radius = 12; Tag = "surface";
        var heading = new Label { Text = "Approval required  ·  " + title, AutoEllipsis = true, Font = new Font("Segoe UI Semibold", 10), Location = new Point(16, 12), Size = new Size(680, 22), Tag = "text" };
        var action = new Label { Text = detail, AutoEllipsis = true, Font = Theme.Mono, Location = new Point(16, 40), Size = new Size(680, 35), Tag = "muted" };
        var deny = Button("Deny", false); deny.Location = new Point(500, 82);
        var approve = Button("Approve once", true); approve.Location = new Point(594, 82); approve.Width = 110;
        deny.Click += (_, _) => Resolve(false); approve.Click += (_, _) => Resolve(true);
        Controls.Add(heading); Controls.Add(action); Controls.Add(deny); Controls.Add(approve);
    }

    private static ModernButton Button(string text, bool accent) => new()
    {
        Text = text, Size = new Size(86, 32), Radius = 8, Tag = accent ? "accent" : "surface", DrawBorder = !accent,
        NormalColor = accent ? Theme.Accent : Theme.Surface, HoverColor = accent ? Theme.AccentHover : Theme.SurfaceHover,
        BorderColor = accent ? Theme.Accent : Theme.Border, ForeColor = accent ? Color.White : Theme.Text
    };

    private void Resolve(bool approved)
    {
        if (!completion.TrySetResult(approved)) return;
        Enabled = false; Visible = false;
    }
}
