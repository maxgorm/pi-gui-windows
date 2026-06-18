using System.Diagnostics;
using System.Text.Json;

namespace PiGUI;

internal sealed class AccountsPanel : Panel
{
    private readonly OAuthService oauth = new();
    private readonly Label codexStatus = new();
    private readonly Label copilotStatus = new();
    private readonly Label detail = new();
    private readonly ModernButton codexButton = new();
    private readonly ModernButton copilotButton = new();
    private CancellationTokenSource? loginCancellation;
    public event Action? CloseRequested;
    public event Action? AccountsChanged;

    public AccountsPanel()
    {
        Dock = DockStyle.Fill; Tag = "background"; Padding = new Padding(40);
        var title = new Label { Text = "Accounts + settings", Font = new Font("Segoe UI Semibold", 20), AutoSize = true, Location = new Point(40, 34), Tag = "text" };
        var subtitle = new Label { Text = "Connect providers and manage Pi GUI without leaving your workspace.", AutoSize = true, Location = new Point(42, 76), Tag = "muted" };
        var close = MakeButton("Back to chat"); close.Location = new Point(40, 112); close.Click += (_, _) => CloseRequested?.Invoke();
        Controls.Add(title); Controls.Add(subtitle); Controls.Add(close);
        AddProviderRow("Codex", "ChatGPT Plus / Pro", "openai-codex", 172, codexStatus, codexButton);
        AddProviderRow("GitHub Copilot", "GitHub account with Copilot", "github-copilot", 254, copilotStatus, copilotButton);
        detail.Location = new Point(42, 348); detail.Size = new Size(650, 60); detail.Tag = "muted"; detail.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(detail); RefreshStatuses();
        VisibleChanged += (_, _) => { if (Visible) RefreshStatuses(); else loginCancellation?.Cancel(); };
    }

    private void AddProviderRow(string name, string description, string provider, int top, Label status, ModernButton button)
    {
        var panel = new RoundedPanel { Location = new Point(40, top), Size = new Size(650, 68), Tag = "surface", Radius = 10 };
        var nameLabel = new Label { Text = name, Font = new Font("Segoe UI Semibold", 11), AutoSize = true, Location = new Point(16, 10), Tag = "text" };
        var descriptionLabel = new Label { Text = description, AutoSize = true, Location = new Point(17, 39), Tag = "muted" };
        status.AutoSize = true; status.Location = new Point(410, 26); status.Tag = "muted";
        button.Text = "Connect"; button.Location = new Point(546, 17); button.Size = new Size(88, 34); button.Tag = "surface";
        button.Click += async (_, _) => await ConnectAsync(provider, button);
        panel.Controls.Add(nameLabel); panel.Controls.Add(descriptionLabel); panel.Controls.Add(status); panel.Controls.Add(button); Controls.Add(panel);
    }

    private async Task ConnectAsync(string provider, ModernButton source)
    {
        codexButton.Enabled = copilotButton.Enabled = false; source.Text = "Opening…"; detail.Text = "Preparing a secure browser sign-in…";
        loginCancellation = new CancellationTokenSource();
        try { await oauth.ConnectAsync(provider, HandleOAuthEvent, loginCancellation.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Ui(() => detail.Text = ex.Message); }
        finally { Ui(() => { codexButton.Enabled = copilotButton.Enabled = true; codexButton.Text = copilotButton.Text = "Connect"; RefreshStatuses(); }); }
    }

    private void HandleOAuthEvent(JsonElement message) => Ui(() =>
    {
        switch (message.GetProperty("event").GetString())
        {
            case "open_url":
                detail.Text = "Complete the Codex sign-in in your browser; this panel will update automatically.";
                Process.Start(new ProcessStartInfo(message.GetProperty("url").GetString()!) { UseShellExecute = true }); break;
            case "device_code":
                var code = message.GetProperty("userCode").GetString()!; Clipboard.SetText(code);
                detail.Text = $"Copilot code {code} was copied. Paste it into the GitHub page that opened.";
                Process.Start(new ProcessStartInfo(message.GetProperty("verificationUri").GetString()!) { UseShellExecute = true }); break;
            case "progress": detail.Text = message.GetProperty("message").GetString(); break;
            case "complete": detail.Text = "Connected successfully."; RefreshStatuses(); AccountsChanged?.Invoke(); break;
            case "error": detail.Text = message.GetProperty("message").GetString(); break;
        }
    });

    public void RefreshStatuses()
    {
        SetStatus(codexStatus, OAuthService.IsConnected("openai-codex"));
        SetStatus(copilotStatus, OAuthService.IsConnected("github-copilot"));
    }

    private static void SetStatus(Label label, bool connected) { label.Text = connected ? "● Connected" : "○ Not connected"; label.ForeColor = connected ? Theme.Success : Theme.Muted; }
    private static ModernButton MakeButton(string text) => new() { Text = text, Size = new Size(110, 36), Tag = "surface", ForeColor = Theme.Text, NormalColor = Theme.Surface, HoverColor = Theme.SurfaceHover, BorderColor = Theme.Border };
    private void Ui(Action action) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(action); else action(); }
}
