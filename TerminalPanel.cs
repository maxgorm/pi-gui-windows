using System.Diagnostics;
using System.Text;

namespace PiGUI;

internal sealed class TerminalPanel : Panel
{
    private readonly RichTextBox terminal = new();
    private readonly Label title = new();
    private readonly Queue<string> pendingCommands = new();
    private Process? process;
    private int promptStart;
    private int inputStart;
    public event Action? CloseRequested;

    public TerminalPanel()
    {
        Dock = DockStyle.Fill; Tag = "terminal"; Padding = new Padding(12, 8, 12, 10);
        title.Text = "TERMINAL"; title.Font = new Font("Segoe UI Semibold", 8); title.Location = new Point(14, 8); title.AutoSize = true; title.Tag = "muted";
        var close = new ModernButton { Text = "×", Size = new Size(30, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Tag = "terminal", DrawBorder = false };
        close.Click += (_, _) => CloseRequested?.Invoke(); Controls.Add(close);

        terminal.BorderStyle = BorderStyle.None; terminal.Font = Theme.Mono; terminal.BackColor = Theme.Terminal; terminal.ForeColor = Theme.Text;
        terminal.Tag = "terminal"; terminal.AcceptsTab = true; terminal.DetectUrls = false; terminal.WordWrap = false;
        terminal.Location = new Point(14, 36); terminal.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        terminal.KeyDown += TerminalKeyDown; terminal.MouseDown += (_, _) => BeginInvoke(KeepCaretInInput);
        Controls.Add(title); Controls.Add(terminal);
        MouseDown += (_, _) => terminal.Focus();
        Resize += (_, _) => LayoutChildren(); LayoutChildren();
    }

    public void Start(string workingDirectory)
    {
        Stop(); pendingCommands.Clear(); title.Text = $"TERMINAL   {workingDirectory}";
        terminal.Clear(); terminal.SelectionColor = Theme.Text;
        terminal.AppendText($"PowerShell · {workingDirectory}{Environment.NewLine}");
        ShowPrompt();

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe", WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            Arguments = "-NoLogo -NoProfile -NoExit -Command \"[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)\""
        };
        process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendOutput(e.Data + Environment.NewLine); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendOutput(e.Data + Environment.NewLine, true); };
        process.Exited += (_, _) => AppendOutput("[terminal exited]" + Environment.NewLine, true);
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        BeginInvoke(() => { terminal.Focus(); terminal.SelectionStart = terminal.TextLength; });
    }

    public void Stop()
    {
        var owner = process; process = null; if (owner is null) return;
        try { if (!owner.HasExited) owner.Kill(true); } catch { } owner.Dispose();
    }

    private void TerminalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C && terminal.SelectionLength > 0) return;
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true; SubmitCommand(); return;
        }
        if (e.KeyCode == Keys.Home) { e.SuppressKeyPress = true; terminal.SelectionStart = inputStart; return; }
        if ((e.KeyCode is Keys.Back or Keys.Delete) && (terminal.SelectionStart < inputStart || terminal.SelectionStart == inputStart && terminal.SelectionLength == 0))
        {
            e.SuppressKeyPress = true; return;
        }
        if (!e.Control && !e.Alt && terminal.SelectionStart < inputStart)
        {
            terminal.SelectionStart = terminal.TextLength; terminal.SelectionLength = 0;
        }
    }

    private void SubmitCommand()
    {
        var command = terminal.Text[inputStart..].TrimEnd('\r', '\n');
        terminal.SelectionStart = terminal.TextLength; terminal.SelectionColor = Theme.Text; terminal.AppendText(Environment.NewLine);
        if (!string.IsNullOrWhiteSpace(command)) pendingCommands.Enqueue(command);
        ShowPrompt();
        if (string.IsNullOrWhiteSpace(command)) return;
        try { process?.StandardInput.WriteLine(command); process?.StandardInput.Flush(); }
        catch (Exception ex) { AppendOutput(ex.Message + Environment.NewLine, true); }
    }

    private void AppendOutput(string text, bool error = false)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => AppendOutput(text, error)); return; }
        var line = text.TrimEnd('\r', '\n');
        if (pendingCommands.Count > 0 && line.StartsWith("PS ", StringComparison.OrdinalIgnoreCase) && line.EndsWith(pendingCommands.Peek(), StringComparison.OrdinalIgnoreCase))
        {
            pendingCommands.Dequeue(); return;
        }

        var currentInput = inputStart <= terminal.TextLength ? terminal.Text[inputStart..] : "";
        terminal.Select(promptStart, terminal.TextLength - promptStart); terminal.SelectedText = "";
        terminal.SelectionStart = terminal.TextLength; terminal.SelectionColor = error ? Color.FromArgb(235, 112, 112) : Theme.Text;
        terminal.AppendText(text); ShowPrompt(currentInput); terminal.ScrollToCaret();
    }

    private void ShowPrompt(string currentInput = "")
    {
        promptStart = terminal.TextLength; terminal.SelectionColor = Theme.Success; terminal.AppendText("❯ ");
        inputStart = terminal.TextLength; terminal.SelectionColor = Theme.Text;
        if (currentInput.Length > 0) terminal.AppendText(currentInput);
        terminal.SelectionStart = terminal.TextLength; terminal.SelectionLength = 0;
    }

    private void KeepCaretInInput()
    {
        if (terminal.SelectionLength == 0 && terminal.SelectionStart < inputStart) terminal.SelectionStart = terminal.TextLength;
    }

    private void LayoutChildren()
    {
        foreach (var button in Controls.OfType<ModernButton>()) button.Location = new Point(ClientSize.Width - 44, 4);
        terminal.Size = new Size(Math.Max(100, ClientSize.Width - 28), Math.Max(60, ClientSize.Height - 48));
    }

    protected override void Dispose(bool disposing) { if (disposing) Stop(); base.Dispose(disposing); }
}
