using System.Diagnostics;
using System.Text;

namespace PiGUI;

internal sealed class TerminalPanel : Panel
{
    private readonly RichTextBox output = new();
    private readonly TextBox input = new();
    private readonly Label title = new();
    private readonly Label prompt = new();
    private Process? process;
    public event Action? CloseRequested;

    public TerminalPanel()
    {
        Dock = DockStyle.Fill; Tag = "terminal"; Padding = new Padding(12, 8, 12, 10);
        title.Text = "TERMINAL"; title.Font = new Font("Segoe UI Semibold", 8); title.Location = new Point(14, 8); title.AutoSize = true; title.Tag = "muted";
        var close = new ModernButton { Text = "×", Size = new Size(30, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = "terminal", DrawBorder = false };
        close.Location = new Point(Width - 44, 4); close.Click += (_, _) => CloseRequested?.Invoke(); Controls.Add(close);
        output.ReadOnly = true; output.BorderStyle = BorderStyle.None; output.Font = Theme.Mono; output.BackColor = Theme.Terminal; output.ForeColor = Theme.Text; output.Tag = "terminal";
        output.Location = new Point(14, 72); output.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; output.Cursor = Cursors.IBeam;
        output.MouseDown += (_, _) => input.Focus();
        input.BorderStyle = BorderStyle.FixedSingle; input.Font = Theme.Mono; input.BackColor = Theme.TerminalInput; input.ForeColor = Theme.Text; input.Tag = "terminal-input";
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; input.KeyDown += InputKeyDown;
        prompt.Text = "❯"; prompt.Font = new Font(Theme.Mono, FontStyle.Bold); prompt.ForeColor = Theme.Success; prompt.TextAlign = ContentAlignment.MiddleCenter; prompt.Tag = "terminal";
        prompt.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Controls.Add(title); Controls.Add(output); Controls.Add(prompt); Controls.Add(input);
        MouseDown += (_, _) => input.Focus();
        Resize += (_, _) => LayoutChildren(); LayoutChildren();
    }

    public void Start(string workingDirectory)
    {
        Stop(); output.Clear(); title.Text = $"TERMINAL   {workingDirectory}";
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe", WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            Arguments = "-NoLogo -NoProfile -NoExit -Command \"[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)\""
        };
        process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Append(e.Data + Environment.NewLine); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Append(e.Data + Environment.NewLine, true); };
        process.Exited += (_, _) => Append("[terminal exited]" + Environment.NewLine, true);
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        Append($"PowerShell · {workingDirectory}{Environment.NewLine}Click anywhere in the terminal and type a command below.{Environment.NewLine}");
        BeginInvoke(() => input.Focus());
    }

    public void Stop()
    {
        var owner = process; process = null; if (owner is null) return;
        try { if (!owner.HasExited) owner.Kill(true); } catch { } owner.Dispose();
    }

    private void InputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return; e.SuppressKeyPress = true;
        var command = input.Text; input.Clear(); if (string.IsNullOrWhiteSpace(command)) return;
        try { process?.StandardInput.WriteLine(command); process?.StandardInput.Flush(); }
        catch (Exception ex) { Append(ex.Message + Environment.NewLine, true); }
    }

    private void Append(string text, bool error = false)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Append(text, error)); return; }
        output.SelectionStart = output.TextLength; output.SelectionColor = error ? Color.FromArgb(235, 112, 112) : Theme.Text;
        output.AppendText(text); output.SelectionColor = Theme.Text; output.ScrollToCaret();
    }

    private void LayoutChildren()
    {
        foreach (var button in Controls.OfType<ModernButton>()) button.Location = new Point(ClientSize.Width - 44, 4);
        prompt.Location = new Point(14, 36); prompt.Size = new Size(24, 28);
        input.Location = new Point(40, 36); input.Size = new Size(Math.Max(100, ClientSize.Width - 54), 28);
        output.Size = new Size(Math.Max(100, ClientSize.Width - 28), Math.Max(40, ClientSize.Height - 82));
    }

    protected override void Dispose(bool disposing) { if (disposing) Stop(); base.Dispose(disposing); }
}
