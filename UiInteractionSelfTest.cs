using System.Reflection;

namespace PiGUI;

internal static class UiInteractionSelfTest
{
    public static int Run()
    {
        var errorPath = Path.Combine(Path.GetTempPath(), "PiGUI-ui-stress-error.txt");
        try { File.Delete(errorPath); } catch { }
        Exception? failure = null;
        var changed = false;
        using var form = new Form { ShowInTaskbar = false, Opacity = 0, Size = new Size(320, 160) };
        var dropdown = new ModernDropdown { Location = new Point(20, 20), Width = 180 };
        dropdown.Items.AddRange(new object[] { "Codex", "GitHub Copilot" });
        dropdown.SelectedItem = "Codex";
        dropdown.SelectedIndexChanged += (_, _) => changed = true;
        form.Controls.Add(dropdown);

        ThreadExceptionEventHandler threadException = (_, e) => { failure = e.Exception; form.Close(); };
        Application.ThreadException += threadException;
        form.Shown += (_, _) => form.BeginInvoke(() =>
        {
            try
            {
                typeof(ModernDropdown).GetMethod("ShowMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dropdown, null);
                var menu = (ContextMenuStrip?)typeof(ModernDropdown).GetField("activeMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dropdown)
                    ?? throw new InvalidOperationException("The drop-down menu did not open.");
                menu.Items[1].PerformClick();
                if (!menu.IsDisposed) menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop(); timer.Dispose();
                    if (!changed || !Equals(dropdown.SelectedItem, "GitHub Copilot"))
                        failure = new InvalidOperationException("The drop-down selection did not change.");
                    if (!menu.IsDisposed)
                        failure = new InvalidOperationException("The closed drop-down menu was not cleaned up.");
                    form.Close();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                failure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                form.Close();
            }
        });

        Application.Run(form);
        Application.ThreadException -= threadException;
        if (failure is not null)
        {
            try { File.WriteAllText(errorPath, failure.ToString()); } catch { }
        }
        return failure is null ? 0 : 1;
    }
}
