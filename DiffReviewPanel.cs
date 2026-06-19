namespace PiGUI;

internal sealed class DiffReviewPanel : Panel
{
    private readonly Label title = new();
    private ChangeCountLabel totals = new(0, 0);
    private readonly RichTextBox diff = new();
    private readonly ModernDropdown files = new();
    private TurnChanges? changes;
    public event Action? CloseRequested;

    public DiffReviewPanel()
    {
        Dock = DockStyle.Fill; Tag = "background"; Padding = new Padding(28);
        title.Text = "Review changes"; title.Font = new Font("Segoe UI Semibold", 18F); title.Location = new Point(30, 24); title.AutoSize = true; title.Tag = "text";
        totals.Location = new Point(31, 55); totals.Size = new Size(220, 24);
        var back = new ModernButton { Text = "Back to chat", Location = new Point(30, 88), Size = new Size(110, 36), Radius = 9, Tag = "surface" };
        back.Click += (_, _) => CloseRequested?.Invoke();
        files.Location = new Point(152, 90); files.Size = new Size(310, 32); files.Tag = "surface";
        files.SelectedIndexChanged += (_, _) => ScrollToSelectedFile();
        diff.ReadOnly = true; diff.BorderStyle = BorderStyle.None; diff.Font = Theme.Mono; diff.WordWrap = false; diff.ScrollBars = RichTextBoxScrollBars.None;
        diff.Location = new Point(30, 140); diff.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; diff.Tag = "diff";
        Controls.Add(title); Controls.Add(totals); Controls.Add(back); Controls.Add(files); Controls.Add(diff);
        Resize += (_, _) => diff.Size = new Size(Math.Max(200, ClientSize.Width - 60), Math.Max(120, ClientSize.Height - 166));
    }

    public void SetChanges(TurnChanges value)
    {
        changes = value; title.Text = $"Review {value.Files.Count} changed {(value.Files.Count == 1 ? "file" : "files")}";
        Controls.Remove(totals); totals.Dispose(); totals = new ChangeCountLabel(value.Additions, value.Deletions) { Location = new Point(31, 55), Size = new Size(220, 24) }; Controls.Add(totals); totals.BringToFront();
        files.Items.Clear(); files.Items.Add("All files"); foreach (var file in value.Files) files.Items.Add(file.Path); files.SelectedItem = "All files";
        RenderPatch(value.Patch);
    }

    private void RenderPatch(string patch)
    {
        diff.SuspendLayout(); diff.Clear();
        foreach (var line in patch.Replace("\r\n", "\n").Split('\n'))
        {
            diff.SelectionStart = diff.TextLength; diff.SelectionLength = 0;
            diff.SelectionBackColor = Theme.Background;
            diff.SelectionColor = line.StartsWith("+++") || line.StartsWith("---") ? Theme.Muted
                : line.StartsWith("+") ? Theme.Success
                : line.StartsWith("-") ? Color.FromArgb(235, 104, 112)
                : line.StartsWith("@@") ? Theme.Accent : Theme.Text;
            diff.AppendText(line + "\n");
        }
        diff.SelectionStart = 0; diff.ResumeLayout();
    }

    private void ScrollToSelectedFile()
    {
        if (changes is null || files.SelectedItem is not string selected || selected == "All files") { diff.SelectionStart = 0; diff.ScrollToCaret(); return; }
        var index = diff.Text.IndexOf("+++ b/" + selected, StringComparison.Ordinal);
        if (index < 0) index = diff.Text.IndexOf(selected, StringComparison.Ordinal);
        if (index >= 0) { diff.SelectionStart = index; diff.SelectionLength = 0; diff.ScrollToCaret(); }
    }
}
