namespace PiGUI;

internal sealed class ApprovalForm : Form
{
    public bool Approved { get; private set; }

    public ApprovalForm(string title, string detail)
    {
        Text = "Approval required"; Size = new Size(520, 280); MinimumSize = Size; MaximumSize = Size;
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
        BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Ui; ShowInTaskbar = false;

        var icon = new Label { Text = "!", Font = new Font("Segoe UI Semibold", 18), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Theme.Accent, Size = new Size(38, 38), Location = new Point(28, 25) };
        var heading = new Label { Text = title, Font = new Font("Segoe UI Semibold", 15), AutoSize = true, Location = new Point(80, 28) };
        var hint = new Label { Text = "Pi wants to perform this action", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(82, 58) };
        var card = new RoundedPanel { Location = new Point(28, 92), Size = new Size(450, 78), BackColor = Theme.Surface, BorderColor = Theme.Border, Radius = 10 };
        var action = new Label { Text = detail, ForeColor = Theme.Text, Font = Theme.Mono, AutoEllipsis = true, AutoSize = false, Location = new Point(14, 12), Size = new Size(420, 52) };
        card.Controls.Add(action);

        var deny = new ModernButton { Text = "Deny", Location = new Point(278, 190), Size = new Size(92, 38), NormalColor = Theme.Surface, HoverColor = Theme.SurfaceHover, BorderColor = Theme.Border, ForeColor = Theme.Text };
        var approve = new ModernButton { Text = "Approve once", Location = new Point(378, 190), Size = new Size(100, 38), NormalColor = Theme.Accent, HoverColor = Theme.AccentHover, BorderColor = Theme.Accent, ForeColor = Color.White };
        deny.Click += (_, _) => { Approved = false; DialogResult = DialogResult.Cancel; Close(); };
        approve.Click += (_, _) => { Approved = true; DialogResult = DialogResult.OK; Close(); };
        Controls.Add(icon); Controls.Add(heading); Controls.Add(hint); Controls.Add(card); Controls.Add(deny); Controls.Add(approve);
        AcceptButton = approve; CancelButton = deny;
    }
}
