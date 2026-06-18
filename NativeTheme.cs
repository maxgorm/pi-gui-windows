using System.Runtime.InteropServices;

namespace PiGUI;

internal static class NativeTheme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    public static void Apply(Form form)
    {
        if (!OperatingSystem.IsWindows()) return;
        var enabled = Theme.IsDark ? 1 : 0;
        try { DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)); } catch { }
        ApplyControl(form);
    }

    private static void ApplyControl(Control control)
    {
        if (control.IsHandleCreated)
        {
            try { SetWindowTheme(control.Handle, Theme.IsDark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
        }
        foreach (Control child in control.Controls) ApplyControl(child);
    }
}
