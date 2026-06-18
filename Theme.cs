namespace PiGUI;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(17, 18, 20);
    public static readonly Color Sidebar = Color.FromArgb(12, 13, 15);
    public static readonly Color Surface = Color.FromArgb(29, 30, 34);
    public static readonly Color SurfaceHover = Color.FromArgb(39, 41, 46);
    public static readonly Color Border = Color.FromArgb(52, 54, 61);
    public static readonly Color Text = Color.FromArgb(238, 238, 240);
    public static readonly Color Muted = Color.FromArgb(157, 160, 170);
    public static readonly Color Accent = Color.FromArgb(237, 102, 80);
    public static readonly Color UserBubble = Color.FromArgb(45, 47, 53);
    public static readonly Font Ui = new("Segoe UI", 10F);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font Mono = new("Cascadia Mono", 9F);
}
