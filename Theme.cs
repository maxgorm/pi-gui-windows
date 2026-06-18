namespace PiGUI;

internal static class Theme
{
    public static bool IsDark { get; private set; } = true;
    public static Color Background => IsDark ? Color.FromArgb(18, 19, 22) : Color.FromArgb(249, 250, 252);
    public static Color Sidebar => Background;
    public static Color Surface => IsDark ? Color.FromArgb(30, 32, 37) : Color.White;
    public static Color SurfaceAlt => IsDark ? Color.FromArgb(35, 37, 43) : Color.FromArgb(246, 247, 250);
    public static Color SurfaceHover => IsDark ? Color.FromArgb(44, 47, 54) : Color.FromArgb(229, 232, 238);
    public static Color Border => IsDark ? Color.FromArgb(59, 62, 70) : Color.FromArgb(218, 221, 228);
    public static Color Text => IsDark ? Color.FromArgb(240, 241, 244) : Color.FromArgb(28, 30, 36);
    public static Color Muted => IsDark ? Color.FromArgb(156, 160, 171) : Color.FromArgb(104, 109, 121);
    public static Color Accent => IsDark ? Color.FromArgb(120, 113, 255) : Color.FromArgb(91, 82, 225);
    public static Color AccentHover => IsDark ? Color.FromArgb(139, 132, 255) : Color.FromArgb(76, 67, 205);
    public static Color UserBubble => IsDark ? Color.FromArgb(42, 45, 52) : Color.FromArgb(238, 240, 246);
    public static Color AssistantBubble => IsDark ? Color.FromArgb(25, 27, 31) : Color.White;
    public static Color Success => IsDark ? Color.FromArgb(107, 210, 151) : Color.FromArgb(27, 143, 83);
    public static Color Warning => IsDark ? Color.FromArgb(255, 172, 92) : Color.FromArgb(201, 103, 18);
    public static Color Terminal => IsDark ? Color.FromArgb(14, 15, 18) : Color.FromArgb(246, 247, 250);
    public static Color TerminalInput => IsDark ? Color.FromArgb(24, 26, 30) : Color.White;
    public static readonly Font Ui = new("Segoe UI", 10F);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font Mono = new("Cascadia Mono", 9F);

    public static void SetMode(string mode) => IsDark = !string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase);
}
