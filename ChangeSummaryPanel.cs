namespace PiGUI;

internal sealed class ChangeSummaryPanel : RoundedPanel
{
    private readonly ModernButton undo = new();
    private readonly ModernButton review = new();
    private readonly Label heading = new();
    public TurnChanges Changes { get; }
    public event Action<ChangeSummaryPanel>? ReviewRequested;
    public event Action<ChangeSummaryPanel>? UndoRequested;

    public ChangeSummaryPanel(TurnChanges changes)
    {
        Changes = changes; Width = 720; Radius = 12; Tag = "surface"; Margin = new Padding(4, 4, 0, 12);
        var visibleFiles = changes.Files.Take(4).ToList(); Height = 60 + visibleFiles.Count * 30 + (changes.Files.Count > 4 ? 24 : 0);
        heading.Text = $"Edited {changes.Files.Count} {(changes.Files.Count == 1 ? "file" : "files")}"; heading.Font = new Font("Segoe UI Semibold", 10F); heading.Location = new Point(46, 14); heading.AutoSize = true; heading.Tag = "text";
        var totals = new ChangeCountLabel(changes.Additions, changes.Deletions) { Location = new Point(46, 34), Size = new Size(130, 20) };
        var icon = new Label { Text = "↕", Font = new Font("Segoe UI Semibold", 13F), TextAlign = ContentAlignment.MiddleCenter, Location = new Point(12, 12), Size = new Size(28, 30), Tag = "muted" };
        ConfigureButton(undo, "Undo", 82); ConfigureButton(review, "Review", 76);
        undo.Click += (_, _) => UndoRequested?.Invoke(this); review.Click += (_, _) => ReviewRequested?.Invoke(this);
        Controls.Add(icon); Controls.Add(heading); Controls.Add(totals); Controls.Add(undo); Controls.Add(review);
        var y = 58;
        foreach (var file in visibleFiles)
        {
            var name = new Label { Text = file.Path, AutoEllipsis = true, Location = new Point(16, y), Size = new Size(Width - 150, 24), Tag = "text" };
            var count = new ChangeCountLabel(file.Additions, file.Deletions) { Location = new Point(Width - 126, y), Size = new Size(108, 24), TextAlign = ContentAlignment.MiddleRight };
            Controls.Add(name); Controls.Add(count); y += 30;
        }
        if (changes.Files.Count > visibleFiles.Count)
            Controls.Add(new Label { Text = $"+ {changes.Files.Count - visibleFiles.Count} more files", Location = new Point(16, y), AutoSize = true, Tag = "muted" });
        Resize += (_, _) => LayoutHeader(); LayoutHeader();
    }

    public void MarkUndone()
    {
        heading.Text = "Changes undone"; undo.Enabled = false; undo.Text = "Undone";
    }

    public void SetUndoBusy(bool busy) { undo.Enabled = !busy; undo.Text = busy ? "Undoing…" : "Undo"; }

    private void LayoutHeader() { review.Location = new Point(Width - 88, 13); undo.Location = new Point(Width - 178, 13); }
    private static void ConfigureButton(ModernButton button, string text, int width)
    {
        button.Text = text; button.Size = new Size(width, 32); button.Radius = 8; button.Tag = "surface";
        button.NormalColor = Theme.Surface; button.HoverColor = Theme.SurfaceHover; button.BorderColor = Theme.Border; button.ForeColor = Theme.Text;
    }
}

internal sealed class ChangeCountLabel : Control
{
    private readonly int additions;
    private readonly int deletions;
    public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleLeft;
    public ChangeCountLabel(int additions, int deletions) { this.additions = additions; this.deletions = deletions; Font = Theme.Small; Tag = "transparent-muted"; SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var add = $"+{additions}"; var del = $"-{deletions}"; var addSize = TextRenderer.MeasureText(add, Font); var delSize = TextRenderer.MeasureText(del, Font);
        var total = addSize.Width + delSize.Width + 6; var x = TextAlign == ContentAlignment.MiddleRight ? Width - total : 0; var y = Math.Max(0, (Height - addSize.Height) / 2);
        TextRenderer.DrawText(e.Graphics, add, Font, new Point(x, y), Theme.Success); TextRenderer.DrawText(e.Graphics, del, Font, new Point(x + addSize.Width + 6, y), Color.FromArgb(235, 104, 112));
    }
}
