using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PiGUI;

internal sealed class SmoothFlowLayoutPanel : FlowLayoutPanel
{
    public SmoothFlowLayoutPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
    }
}

internal sealed class ScrollbarlessFlowLayoutPanel : FlowLayoutPanel
{
    [DllImport("user32.dll")] private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
    private const int SbVert = 1;
    private bool draggingThumb;
    public bool DrawScrollIndicator { get; set; }

    public ScrollbarlessFlowLayoutPanel()
    {
        AutoScroll = true; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        ControlAdded += (_, _) => HideScrollbarSoon();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); HideScrollbarSoon(); }
    protected override void OnLayout(LayoutEventArgs levent) { base.OnLayout(levent); HideScrollbarSoon(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); HideScrollbarSoon(); }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!DrawScrollIndicator || !TryGetThumb(out var track, out var thumb)) return;
        using var trackBrush = new SolidBrush(Theme.IsDark ? Color.FromArgb(31, 33, 38) : Color.FromArgb(229, 232, 238));
        using var thumbBrush = new SolidBrush(Theme.IsDark ? Color.FromArgb(82, 86, 96) : Color.FromArgb(154, 159, 170));
        e.Graphics.FillRectangle(trackBrush, track); e.Graphics.FillRectangle(thumbBrush, thumb);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (DrawScrollIndicator && TryGetThumb(out var track, out var thumb) && track.Contains(e.Location))
        {
            draggingThumb = true; Capture = true; SetScrollFromMouse(e.Y, track, thumb.Height); return;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (draggingThumb && TryGetThumb(out var track, out var thumb)) { SetScrollFromMouse(e.Y, track, thumb.Height); return; }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { draggingThumb = false; Capture = false; base.OnMouseUp(e); }

    private bool TryGetThumb(out Rectangle track, out Rectangle thumb)
    {
        track = new Rectangle(Math.Max(0, ClientSize.Width - 7), 3, 5, Math.Max(0, ClientSize.Height - 6)); thumb = Rectangle.Empty;
        var maximum = VerticalScroll.Maximum;
        var large = Math.Max(1, VerticalScroll.LargeChange);
        var range = Math.Max(0, maximum - large + 1);
        if (track.Height <= 0 || range <= 0) return false;
        var height = Math.Max(28, (int)(track.Height * Math.Min(1D, large / (double)(maximum + 1))));
        var travel = Math.Max(1, track.Height - height);
        var top = track.Top + (int)(travel * Math.Min(1D, VerticalScroll.Value / (double)range));
        thumb = new Rectangle(track.X, top, track.Width, Math.Min(height, track.Height)); return true;
    }

    private void SetScrollFromMouse(int mouseY, Rectangle track, int thumbHeight)
    {
        var range = Math.Max(0, VerticalScroll.Maximum - VerticalScroll.LargeChange + 1);
        var travel = Math.Max(1, track.Height - thumbHeight);
        var ratio = Math.Clamp((mouseY - track.Top - thumbHeight / 2D) / travel, 0D, 1D);
        AutoScrollPosition = new Point(0, (int)(range * ratio)); Invalidate();
    }

    private void HideScrollbarSoon()
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(() => { if (!IsDisposed && IsHandleCreated) ShowScrollBar(Handle, SbVert, false); });
    }
}

internal static class Shape
{
    public static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class RoundedPanel : Panel
{
    public int Radius { get; set; } = 14;
    public Color BorderColor { get; set; } = Theme.Border;
    public int BorderWidth { get; set; } = 1;

    public RoundedPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Shape.Rounded(rect, Radius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        if (BorderWidth > 0) { using var pen = new Pen(BorderColor, BorderWidth); e.Graphics.DrawPath(pen, path); }
    }
}

internal class ModernButton : Button
{
    private bool hovering;
    public int Radius { get; set; } = 9;
    public Color NormalColor { get; set; } = Theme.Surface;
    public Color HoverColor { get; set; } = Theme.SurfaceHover;
    public Color BorderColor { get; set; } = Theme.Border;
    public bool DrawBorder { get; set; } = true;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Shape.Rounded(rect, Radius);
        using var brush = new SolidBrush(hovering ? HoverColor : NormalColor);
        e.Graphics.FillPath(brush, path);
        if (DrawBorder) { using var pen = new Pen(BorderColor); e.Graphics.DrawPath(pen, path); }
        var textRect = new Rectangle(rect.X + Padding.Left, rect.Y + Padding.Top,
            Math.Max(1, rect.Width - Padding.Horizontal), Math.Max(1, rect.Height - Padding.Vertical));
        var alignment = TextAlign switch
        {
            ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
            ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter
        };
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ForeColor, alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

internal class ModernComboBox : ComboBox
{
    public ModernComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 25; Font = Theme.Small;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? Theme.SurfaceHover : Theme.Surface);
        e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ModernDropdown : Control
{
    public List<object> Items { get; } = new();
    public Dictionary<string, string> Descriptions { get; } = new();
    private object? selectedItem;
    private bool hovering;
    private ContextMenuStrip? activeMenu;
    public event EventHandler? SelectedIndexChanged;

    public object? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (Equals(selectedItem, value)) return;
            selectedItem = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ModernDropdown()
    {
        Height = 32; Font = Theme.Small; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        MouseEnter += (_, _) => { hovering = true; Invalidate(); }; MouseLeave += (_, _) => { hovering = false; Invalidate(); }; Click += (_, _) => ShowMenu();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Shape.Rounded(rect, 8);
        using var fill = new SolidBrush(hovering && Enabled ? Theme.SurfaceHover : Theme.Surface);
        using var pen = new Pen(Theme.Border);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path);
        var color = Enabled ? Theme.Text : Theme.Muted;
        TextRenderer.DrawText(e.Graphics, selectedItem?.ToString() ?? "Select", Font, new Rectangle(10, 0, Width - 30, Height), color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using var chevron = new Pen(Theme.Muted, 1.5f);
        var cx = Width - 15; var cy = Height / 2;
        e.Graphics.DrawLines(chevron, new[] { new Point(cx - 3, cy - 2), new Point(cx, cy + 1), new Point(cx + 3, cy - 2) });
    }

    private void ShowMenu()
    {
        if (!Enabled || Items.Count == 0) return;
        activeMenu?.Dispose();
        var menu = new ContextMenuStrip { ShowImageMargin = false, BackColor = Theme.Surface, ForeColor = Theme.Text, Renderer = new DarkMenuRenderer(), Font = Theme.Small };
        activeMenu = menu;
        foreach (var item in Items)
        {
            var captured = item;
            var label = item.ToString() ?? "";
            var hasDescription = Descriptions.TryGetValue(label, out var description);
            var entry = new ToolStripMenuItem(hasDescription ? $"{label}\n   {description}" : label)
            {
                Checked = Equals(item, selectedItem), CheckOnClick = false, AutoSize = false,
                Width = hasDescription ? 360 : Math.Max(Width, 150), Height = hasDescription ? 48 : 30
            };
            entry.Click += (_, _) => SelectedItem = captured; menu.Items.Add(entry);
        }
        // Disposing a ToolStripDropDown from its own Closed event leaves WinForms
        // processing the close against an already-disposed native handle. Defer
        // cleanup until the current window message has finished instead.
        menu.Closed += (_, _) =>
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(() =>
            {
                if (ReferenceEquals(activeMenu, menu)) activeMenu = null;
                menu.Dispose();
            });
        };
        menu.Show(this, new Point(0, Height + 3));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { activeMenu?.Dispose(); activeMenu = null; }
        base.Dispose(disposing);
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new MenuColors()) { RoundedEdges = true; }
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color MenuItemSelected => Theme.SurfaceHover;
        public override Color MenuItemBorder => Theme.SurfaceHover;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
    }
}
