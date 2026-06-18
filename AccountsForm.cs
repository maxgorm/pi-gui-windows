using System.Diagnostics;
using System.Text.Json;

namespace PiGUI;

internal sealed class AccountsForm : Form
{
    private readonly OAuthService oauth = new();
    private readonly Label codexStatus = new();
    private readonly Label copilotStatus = new();
    private readonly Label detail = new();
    private readonly Button codexButton = new();
    private readonly Button copilotButton = new();
    private CancellationTokenSource? loginCancellation;

    public event Action? AccountsChanged;

    public AccountsForm()
    {
        Text = "Accounts"; Size = new Size(560, 390); MinimumSize = Size; MaximumSize = Size;
        StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Ui;
        FormClosing += (_, _) => loginCancellation?.Cancel();

        var title = new Label { Text = "Connect your accounts", Font = new Font("Segoe UI Semibold", 18), AutoSize = true, Location = new Point(30, 25) };
        var subtitle = new Label { Text = "Sign in securely in your browser. Pi GUI never sees your password.", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(33, 65) };
        Controls.Add(title); Controls.Add(subtitle);
        AddProviderRow("Codex", "ChatGPT Plus / Pro", "openai-codex", 105, codexStatus, codexButton);
        AddProviderRow("GitHub Copilot", "GitHub account with Copilot", "github-copilot", 185, copilotStatus, copilotButton);
        detail.Location = new Point(32, 280); detail.Size = new Size(490, 52); detail.ForeColor = Theme.Muted; detail.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(detail);
        RefreshStatuses();
    }

    private void AddProviderRow(string name, string description, string provider, int top, Label status, Button button)
    {
        var panel = new Panel { Location = new Point(30, top), Size = new Size(495, 66), BackColor = Theme.Surface };
        var nameLabel = new Label { Text = name, Font = new Font("Segoe UI Semibold", 11), AutoSize = true, Location = new Point(15, 10) };
        var descriptionLabel = new Label { Text = description, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(16, 37) };
        status.AutoSize = true; status.Location = new Point(275, 24);
        button.Text = "Connect"; button.Location = new Point(397, 16); button.Size = new Size(82, 34); StyleButton(button);
        button.Click += async (_, _) => await ConnectAsync(provider, button);
        panel.Controls.Add(nameLabel); panel.Controls.Add(descriptionLabel); panel.Controls.Add(status); panel.Controls.Add(button); Controls.Add(panel);
    }

    private async Task ConnectAsync(string provider, Button source)
    {
        codexButton.Enabled = copilotButton.Enabled = false; source.Text = "Opening…";
        detail.Text = "Preparing a secure browser sign-in…"; loginCancellation = new CancellationTokenSource();
        try
        {
            await oauth.ConnectAsync(provider, HandleOAuthEvent, loginCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Ui(() => detail.Text = ex.Message); }
        finally
        {
            Ui(() => { codexButton.Enabled = copilotButton.Enabled = true; codexButton.Text = "Connect"; copilotButton.Text = "Connect"; RefreshStatuses(); });
        }
    }

    private void HandleOAuthEvent(JsonElement message) => Ui(() =>
    {
        var eventName = message.GetProperty("event").GetString();
        switch (eventName)
        {
            case "open_url":
                var url = message.GetProperty("url").GetString()!;
                detail.Text = "Your browser is open. Complete the Codex sign-in there; this window will update automatically.";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                break;
            case "device_code":
                var code = message.GetProperty("userCode").GetString()!;
                var verification = message.GetProperty("verificationUri").GetString()!;
                Clipboard.SetText(code);
                detail.Text = $"Copilot code {code} was copied. Paste it into the GitHub page that just opened.";
                Process.Start(new ProcessStartInfo(verification) { UseShellExecute = true });
                break;
            case "progress": detail.Text = message.GetProperty("message").GetString(); break;
            case "complete":
                detail.Text = "Connected successfully. You can close this window and choose the provider in chat.";
                RefreshStatuses(); AccountsChanged?.Invoke(); break;
            case "error": detail.Text = message.GetProperty("message").GetString(); break;
        }
    });

    private void RefreshStatuses()
    {
        SetStatus(codexStatus, OAuthService.IsConnected("openai-codex"));
        SetStatus(copilotStatus, OAuthService.IsConnected("github-copilot"));
    }

    private static void SetStatus(Label label, bool connected)
    {
        label.Text = connected ? "● Connected" : "○ Not connected";
        label.ForeColor = connected ? Color.FromArgb(105, 205, 145) : Theme.Muted;
    }

    private void Ui(Action action) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(action); else action(); }
    private static void StyleButton(Button button) { button.FlatStyle = FlatStyle.Flat; button.BackColor = Theme.SurfaceHover; button.ForeColor = Theme.Text; button.FlatAppearance.BorderColor = Theme.Border; button.Cursor = Cursors.Hand; }
}
