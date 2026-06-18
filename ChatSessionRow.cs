namespace PiGUI;

internal sealed class ChatSessionRow : Panel
{
    private readonly ModernButton openButton;
    private readonly ModernButton deleteButton;
    public SavedSession Session { get; }
    public event Action<SavedSession>? OpenRequested;
    public event Action<SavedSession>? DeleteRequested;

    public ChatSessionRow(SavedSession session, string age, bool selected)
    {
        Session = session; Width = 204; Height = 31; Margin = new Padding(10, 0, 0, 1); Tag = "sidebar";
        openButton = new ModernButton
        {
            Text = $"{session.Title}   {age}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 30, 0), DrawBorder = false, AutoEllipsis = true,
            NormalColor = selected ? Theme.SurfaceHover : Theme.Sidebar, HoverColor = Theme.SurfaceHover, ForeColor = Theme.Text,
            Tag = selected ? "sidebar-selected" : "sidebar"
        };
        deleteButton = new ModernButton
        {
            Text = "🗑", Width = 30, Dock = DockStyle.Right, DrawBorder = false, Radius = 7, Visible = false,
            Tag = "delete-chat", NormalColor = Theme.SurfaceHover, HoverColor = Color.FromArgb(115, 52, 57), ForeColor = Theme.Muted
        };
        openButton.Click += (_, _) => OpenRequested?.Invoke(Session);
        deleteButton.Click += (_, _) => DeleteRequested?.Invoke(Session);
        foreach (var control in new Control[] { this, openButton, deleteButton })
        {
            control.MouseEnter += (_, _) => deleteButton.Visible = true;
            control.MouseLeave += (_, _) => BeginInvoke(() => deleteButton.Visible = ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }
        Controls.Add(openButton); Controls.Add(deleteButton); deleteButton.BringToFront();
    }
}
