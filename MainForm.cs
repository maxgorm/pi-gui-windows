using System.Diagnostics;
using System.Text.Json;

namespace PiGUI;

internal sealed class MainForm : Form
{
    private readonly AppSettings settings = AppSettings.Load();
    private readonly PiRpcClient rpc = new();
    private readonly FlowLayoutPanel transcript = new();
    private readonly FlowLayoutPanel attachmentBar = new();
    private readonly FlowLayoutPanel recentProjects = new();
    private readonly ComposerBox composer = new();
    private readonly ComboBox providerBox = new();
    private readonly ComboBox modelBox = new();
    private readonly ComboBox effortBox = new();
    private readonly Button projectButton = new();
    private readonly Button sendButton = new();
    private readonly Button stopButton = new();
    private readonly Label statusLabel = new();
    private readonly Label authLabel = new();
    private readonly List<Attachment> attachments = new();
    private RichTextBox? streamingMessage;
    private bool initialized;

    public MainForm()
    {
        Text = "Pi GUI for Windows";
        MinimumSize = new Size(900, 650);
        Size = new Size(1180, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        AllowDrop = true;

        BuildLayout();
        WireEvents();
        PopulateSettings();
        Shown += async (_, _) => await ConnectAsync();
        FormClosing += (_, _) => { settings.Save(); rpc.DisposeAsync().AsTask().GetAwaiter().GetResult(); };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(14) };
        root.Controls.Add(sidebar, 0, 0);
        var logo = new Label { Text = "π  PI GUI", AutoSize = true, Font = new Font("Segoe UI Semibold", 14), ForeColor = Theme.Text, Location = new Point(15, 18) };
        sidebar.Controls.Add(logo);
        var newChat = MakeButton("＋  New chat", 42);
        newChat.Location = new Point(14, 62); newChat.Width = 200;
        newChat.Click += async (_, _) => await NewChatAsync();
        sidebar.Controls.Add(newChat);
        var projectsTitle = new Label { Text = "PROJECTS", AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8), Location = new Point(17, 122) };
        sidebar.Controls.Add(projectsTitle);
        recentProjects.Location = new Point(8, 145); recentProjects.Width = 214; recentProjects.Height = 360;
        recentProjects.FlowDirection = FlowDirection.TopDown; recentProjects.WrapContents = false; recentProjects.AutoScroll = true; recentProjects.BackColor = Theme.Sidebar;
        sidebar.Controls.Add(recentProjects);

        var connect = MakeButton("Accounts + sign-in", 38);
        connect.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        connect.Location = new Point(14, sidebar.Height - 86); connect.Width = 200;
        connect.Click += (_, _) => OpenAccounts();
        sidebar.Controls.Add(connect);
        sidebar.Resize += (_, _) => { connect.Top = sidebar.ClientSize.Height - 58; authLabel.Top = connect.Top - 27; };
        authLabel.AutoSize = true; authLabel.ForeColor = Theme.Muted; authLabel.Location = new Point(18, 600);
        sidebar.Controls.Add(authLabel);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(0), BackColor = Theme.Background };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 192));
        root.Controls.Add(main, 1, 0);

        var toolbar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(18, 12, 18, 8) };
        main.Controls.Add(toolbar, 0, 0);
        projectButton.TextAlign = ContentAlignment.MiddleLeft; projectButton.AutoEllipsis = true;
        StyleButton(projectButton); projectButton.Location = new Point(18, 12); projectButton.Size = new Size(285, 38);
        toolbar.Controls.Add(projectButton);
        providerBox.DropDownStyle = ComboBoxStyle.DropDownList; StyleCombo(providerBox); providerBox.Location = new Point(320, 12); providerBox.Size = new Size(145, 38);
        toolbar.Controls.Add(providerBox);
        modelBox.DropDownStyle = ComboBoxStyle.DropDownList; StyleCombo(modelBox); modelBox.Location = new Point(475, 12); modelBox.Size = new Size(145, 38);
        toolbar.Controls.Add(modelBox);
        effortBox.DropDownStyle = ComboBoxStyle.DropDownList; StyleCombo(effortBox); effortBox.Location = new Point(630, 12); effortBox.Size = new Size(105, 38);
        toolbar.Controls.Add(effortBox);
        statusLabel.AutoSize = true; statusLabel.ForeColor = Theme.Muted; statusLabel.Location = new Point(750, 22); statusLabel.Text = "Starting…";
        toolbar.Controls.Add(statusLabel);

        transcript.Dock = DockStyle.Fill; transcript.AutoScroll = true; transcript.FlowDirection = FlowDirection.TopDown; transcript.WrapContents = false;
        transcript.BackColor = Theme.Background; transcript.Padding = new Padding(34, 20, 34, 20);
        main.Controls.Add(transcript, 0, 1);

        var composerHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(32, 10, 32, 24) };
        main.Controls.Add(composerHost, 0, 2);
        var composerCard = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12) };
        composerHost.Controls.Add(composerCard);
        attachmentBar.Dock = DockStyle.Top; attachmentBar.Height = 34; attachmentBar.BackColor = Theme.Surface; attachmentBar.WrapContents = false; attachmentBar.AutoScroll = true;
        composerCard.Controls.Add(attachmentBar);
        composer.BorderStyle = BorderStyle.None; composer.BackColor = Theme.Surface; composer.ForeColor = Theme.Text; composer.Font = new Font("Segoe UI", 11);
        composer.Location = new Point(14, 43); composer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        composer.Size = new Size(composerCard.Width - 28, 66); composerCard.Controls.Add(composer);
        var attachButton = MakeButton("＋ Attach", 34); attachButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; attachButton.Location = new Point(12, 116); attachButton.Width = 92;
        attachButton.Click += (_, _) => ChooseFiles(); composerCard.Controls.Add(attachButton);
        sendButton.Text = "Send  ↑"; StyleAccentButton(sendButton); sendButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; sendButton.Size = new Size(92, 36); sendButton.Location = new Point(composerCard.Width - 104, 115); composerCard.Controls.Add(sendButton);
        stopButton.Text = "Stop  ■"; StyleButton(stopButton); stopButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; stopButton.Size = new Size(92, 36); stopButton.Location = sendButton.Location; stopButton.Visible = false; composerCard.Controls.Add(stopButton);
        composerCard.Resize += (_, _) => { composer.Width = composerCard.ClientSize.Width - 28; composer.Height = composerCard.ClientSize.Height - 94; sendButton.Left = composerCard.ClientSize.Width - 104; stopButton.Left = sendButton.Left; attachButton.Top = composerCard.ClientSize.Height - 48; sendButton.Top = attachButton.Top; stopButton.Top = attachButton.Top; };
    }

    private void WireEvents()
    {
        rpc.EventReceived += e => Ui(() => HandleEvent(e));
        rpc.ErrorReceived += text => Ui(() => ShowRuntimeError(text));
        rpc.Exited += () => Ui(() => SetStatus("Disconnected", false));
        projectButton.Click += (_, _) => ChooseProject();
        providerBox.SelectedIndexChanged += async (_, _) => await ChangeProviderAsync();
        modelBox.SelectedIndexChanged += async (_, _) => await ChangeModelAsync();
        effortBox.SelectedIndexChanged += async (_, _) => await ChangeEffortAsync();
        sendButton.Click += async (_, _) => await SendAsync();
        stopButton.Click += async (_, _) => { try { await rpc.SendAsync(new { type = "abort" }); } catch { } };
        composer.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };
        composer.ImagePasted += image => AddClipboardImage(image);
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) AddFiles(files); };
        transcript.Resize += (_, _) => ResizeMessages();
    }

    private void PopulateSettings()
    {
        providerBox.Items.AddRange(new object[] { "Codex", "GitHub Copilot" });
        effortBox.Items.AddRange(new object[] { "low", "medium", "high", "xhigh" });
        providerBox.SelectedItem = settings.Provider == "github-copilot" ? "GitHub Copilot" : "Codex";
        PopulateModels(settings.Model);
        effortBox.SelectedItem = settings.Effort;
        if (!Directory.Exists(settings.ProjectPath)) settings.ProjectPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        projectButton.Text = $"▣  {Path.GetFileName(settings.ProjectPath)}     ⌄";
        RefreshRecentProjects();
        RefreshAuthStatus();
        AddWelcome();
        initialized = true;
    }

    private async Task ConnectAsync()
    {
        SetStatus("Starting pi…", false);
        try
        {
            await rpc.StartAsync(settings.ProjectPath, ProviderId(), ModelId(), settings.Effort);
            var connected = OAuthService.IsConnected(ProviderId());
            SetStatus(connected ? "Ready" : $"{providerBox.SelectedItem} sign-in required", connected);
        }
        catch (Exception ex)
        {
            SetStatus("Setup needed", false);
            AddSystemMessage(ex.Message + "\n\nOpen Accounts + sign-in if this provider is not connected yet.", true);
        }
        RefreshAuthStatus();
    }

    private async Task SendAsync()
    {
        var text = composer.Text.Trim();
        if (text.Length == 0 && attachments.Count == 0) return;
        if (!rpc.IsRunning) { await ConnectAsync(); if (!rpc.IsRunning) return; }
        var outgoing = attachments.ToList();
        AddUserMessage(text.Length == 0 ? "[Attachments]" : text, outgoing);
        composer.Clear(); attachments.Clear(); RefreshAttachments();
        try { await rpc.PromptAsync(text, outgoing); }
        catch (Exception ex) { AddSystemMessage(ex.Message, true); SetBusy(false); }
    }

    private void HandleEvent(JsonElement e)
    {
        if (!e.TryGetProperty("type", out var typeNode)) return;
        var type = typeNode.GetString();
        switch (type)
        {
            case "agent_start": SetBusy(true); EnsureStreamingMessage(); break;
            case "agent_end": SetBusy(false); FinishStreamingMessage(); break;
            case "message_update":
                if (e.TryGetProperty("assistantMessageEvent", out var update) && update.TryGetProperty("type", out var updateType))
                {
                    if (updateType.GetString() == "text_delta" && update.TryGetProperty("delta", out var delta)) AppendStream(delta.GetString() ?? "");
                    else if (updateType.GetString() == "thinking_delta") statusLabel.Text = "Thinking…";
                    else if (updateType.GetString() == "error" && update.TryGetProperty("error", out var error)) AddSystemMessage(error.ToString(), true);
                }
                break;
            case "tool_execution_start":
                var tool = e.TryGetProperty("toolName", out var toolName) ? toolName.GetString() : "tool";
                AddToolMessage($"Running {tool}…"); break;
            case "tool_execution_end":
                var endedTool = e.TryGetProperty("toolName", out var endedName) ? endedName.GetString() : "tool";
                var failed = e.TryGetProperty("isError", out var isError) && isError.GetBoolean();
                AddToolMessage($"{endedTool} {(failed ? "failed" : "finished")}"); break;
            case "auto_retry_start": statusLabel.Text = "Retrying…"; break;
            case "compaction_start": statusLabel.Text = "Compacting context…"; break;
        }
    }

    private void EnsureStreamingMessage()
    {
        if (streamingMessage is not null) return;
        streamingMessage = CreateMessageBox("", false);
        transcript.Controls.Add(WrapMessage("PI", streamingMessage, false));
        ScrollToBottom();
    }

    private void AppendStream(string text)
    {
        EnsureStreamingMessage();
        streamingMessage!.AppendText(text);
        streamingMessage.Height = Math.Min(800, Math.Max(40, TextRenderer.MeasureText(streamingMessage.Text + "\n", streamingMessage.Font, new Size(streamingMessage.Width, int.MaxValue), TextFormatFlags.WordBreak).Height + 24));
        ScrollToBottom();
    }

    private void FinishStreamingMessage()
    {
        if (streamingMessage is { TextLength: 0 }) streamingMessage.Text = "Done.";
        streamingMessage = null;
        statusLabel.Text = "Ready";
    }

    private void AddWelcome() => AddSystemMessage("What would you like to build?\n\nPi can read and edit files, run commands, and work across the selected project. Paste an image, drop files here, or attach them with the button below.", false);

    private void AddUserMessage(string text, List<Attachment> files)
    {
        var details = files.Count == 0 ? "" : "\n\n" + string.Join("  •  ", files.Select(f => f.Name));
        var box = CreateMessageBox(text + details, true);
        transcript.Controls.Add(WrapMessage("YOU", box, true));
        ScrollToBottom();
    }

    private void AddSystemMessage(string text, bool error)
    {
        var box = CreateMessageBox(text, false);
        if (error) box.ForeColor = Color.FromArgb(255, 135, 125);
        transcript.Controls.Add(WrapMessage(error ? "NOTICE" : "PI", box, false));
        ScrollToBottom();
    }

    private void AddToolMessage(string text)
    {
        var label = new Label { Text = "  ⚙  " + text + "  ", AutoSize = true, ForeColor = Theme.Muted, BackColor = Theme.Surface, Padding = new Padding(5), Margin = new Padding(30, 5, 0, 5), Font = Theme.Small };
        transcript.Controls.Add(label); ScrollToBottom();
    }

    private Control WrapMessage(string author, RichTextBox box, bool user)
    {
        var host = new Panel { Width = Math.Max(400, transcript.ClientSize.Width - 90), Height = box.Height + 30, Margin = new Padding(0, 5, 0, 12), BackColor = Theme.Background };
        var name = new Label { Text = author, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8), AutoSize = true, Location = new Point(user ? 115 : 4, 0) };
        box.Location = new Point(user ? 110 : 0, 23); box.Width = host.Width - (user ? 120 : 20); box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        host.Controls.Add(name); host.Controls.Add(box); return host;
    }

    private RichTextBox CreateMessageBox(string text, bool user)
    {
        var width = Math.Max(360, transcript.ClientSize.Width - (user ? 210 : 110));
        var measured = TextRenderer.MeasureText(text + "\n", Theme.Ui, new Size(width - 24, int.MaxValue), TextFormatFlags.WordBreak);
        return new RichTextBox
        {
            Text = text, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = true, ScrollBars = RichTextBoxScrollBars.None,
            BackColor = user ? Theme.UserBubble : Theme.Background, ForeColor = Theme.Text, Font = Theme.Ui,
            Width = width, Height = Math.Min(800, Math.Max(42, measured.Height + 24)), Padding = new Padding(8), TabStop = false
        };
    }

    private async Task NewChatAsync()
    {
        try { if (rpc.IsRunning) await rpc.SendAsync(new { type = "new_session" }); } catch { }
        transcript.Controls.Clear(); streamingMessage = null; AddWelcome();
    }

    private void ChooseProject()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the project pi should work in", SelectedPath = settings.ProjectPath, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SwitchProject(dialog.SelectedPath);
    }

    private async void SwitchProject(string path)
    {
        settings.RememberProject(path); projectButton.Text = $"▣  {Path.GetFileName(path)}     ⌄"; RefreshRecentProjects();
        transcript.Controls.Clear(); AddWelcome(); await ConnectAsync();
    }

    private void RefreshRecentProjects()
    {
        recentProjects.Controls.Clear();
        foreach (var path in settings.RecentProjects.Prepend(settings.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var button = MakeButton("▣  " + (Path.GetFileName(path) is { Length: > 0 } name ? name : path), 35);
            button.Width = 195; button.TextAlign = ContentAlignment.MiddleLeft; button.Tag = path; button.AutoEllipsis = true;
            button.Click += (_, _) => SwitchProject((string)button.Tag); recentProjects.Controls.Add(button);
        }
    }

    private void ChooseFiles()
    {
        using var dialog = new OpenFileDialog { Multiselect = true, Title = "Attach images or files", Filter = "All files|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        foreach (var file in files.Where(File.Exists))
        {
            var isImage = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(Path.GetExtension(file).ToLowerInvariant());
            if (!attachments.Any(a => string.Equals(a.Path, file, StringComparison.OrdinalIgnoreCase))) attachments.Add(new Attachment(file, isImage));
        }
        RefreshAttachments();
    }

    private void AddClipboardImage(Image image)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PiGUI", "attachments"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"pasted-{DateTime.Now:yyyyMMdd-HHmmssfff}.png"); image.Save(path, System.Drawing.Imaging.ImageFormat.Png); image.Dispose();
        AddFiles(new[] { path });
    }

    private void RefreshAttachments()
    {
        attachmentBar.Controls.Clear();
        foreach (var item in attachments.ToList())
        {
            var chip = MakeButton((item.IsImage ? "▧  " : "▤  ") + item.Name + "  ×", 29); chip.AutoSize = true; chip.Tag = item;
            chip.Click += (_, _) => { attachments.Remove((Attachment)chip.Tag); RefreshAttachments(); }; attachmentBar.Controls.Add(chip);
        }
    }

    private async Task ChangeModelAsync()
    {
        if (modelBox.SelectedItem is null) return; settings.Model = ModelId(); settings.Save();
        if (initialized && rpc.IsRunning) try { await rpc.SendAsync(new { type = "set_model", provider = ProviderId(), modelId = settings.Model }); } catch (Exception ex) { AddSystemMessage(ex.Message, true); }
    }

    private async Task ChangeProviderAsync()
    {
        if (providerBox.SelectedItem is null) return;
        settings.Provider = ProviderId();
        var preferred = settings.Provider == "github-copilot" ? "gpt-5.3-codex" : "gpt-5.5";
        PopulateModels(preferred); settings.Model = ModelId(); settings.Save(); RefreshAuthStatus();
        if (initialized) await ConnectAsync();
    }

    private async Task ChangeEffortAsync()
    {
        if (effortBox.SelectedItem is not string effort) return; settings.Effort = effort; settings.Save();
        if (rpc.IsRunning) try { await rpc.SendAsync(new { type = "set_thinking_level", level = effort }); } catch (Exception ex) { AddSystemMessage(ex.Message, true); }
    }

    private string ProviderId() => providerBox.SelectedItem?.ToString() == "GitHub Copilot" ? "github-copilot" : "openai-codex";
    private string ModelId() => modelBox.SelectedItem?.ToString() switch
    {
        "GPT-5.4" => "gpt-5.4", "GPT-5.4 mini" => "gpt-5.4-mini", "GPT-5.3 Codex" => "gpt-5.3-codex",
        "GPT-5.2 Codex" => "gpt-5.2-codex", "Claude Opus 4.8" => "claude-opus-4.8", _ => "gpt-5.5"
    };

    private void PopulateModels(string preferredId)
    {
        modelBox.Items.Clear();
        if (ProviderId() == "github-copilot") modelBox.Items.AddRange(new object[] { "GPT-5.3 Codex", "GPT-5.2 Codex", "Claude Opus 4.8" });
        else modelBox.Items.AddRange(new object[] { "GPT-5.5", "GPT-5.4", "GPT-5.4 mini" });
        var friendly = preferredId switch
        {
            "gpt-5.4" => "GPT-5.4", "gpt-5.4-mini" => "GPT-5.4 mini", "gpt-5.3-codex" => "GPT-5.3 Codex",
            "gpt-5.2-codex" => "GPT-5.2 Codex", "claude-opus-4.8" => "Claude Opus 4.8", _ => "GPT-5.5"
        };
        modelBox.SelectedItem = modelBox.Items.Contains(friendly) ? friendly : modelBox.Items[0];
    }

    private void OpenAccounts()
    {
        using var dialog = new AccountsForm();
        dialog.AccountsChanged += async () => { RefreshAuthStatus(); await ConnectAsync(); };
        dialog.ShowDialog(this); RefreshAuthStatus();
    }

    private void RefreshAuthStatus()
    {
        var codex = OAuthService.IsConnected("openai-codex"); var copilot = OAuthService.IsConnected("github-copilot");
        authLabel.Text = $"{(codex ? "●" : "○")} Codex   {(copilot ? "●" : "○")} Copilot";
        authLabel.ForeColor = codex || copilot ? Color.FromArgb(105, 205, 145) : Theme.Muted;
    }

    private void SetBusy(bool value)
    {
        sendButton.Visible = !value; stopButton.Visible = value; providerBox.Enabled = !value; modelBox.Enabled = !value; effortBox.Enabled = !value;
        statusLabel.Text = value ? "Working…" : "Ready";
    }

    private void SetStatus(string text, bool connected) { statusLabel.Text = (connected ? "●  " : "○  ") + text; statusLabel.ForeColor = connected ? Color.FromArgb(105, 205, 145) : Theme.Muted; }
    private void ShowRuntimeError(string text) { if (text.Contains("error", StringComparison.OrdinalIgnoreCase) || text.Contains("No API key", StringComparison.OrdinalIgnoreCase)) statusLabel.Text = "Runtime notice"; }
    private void Ui(Action action) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(action); else action(); }
    private void ScrollToBottom()
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() => { if (!IsDisposed && transcript.Controls.Count > 0) transcript.ScrollControlIntoView(transcript.Controls[transcript.Controls.Count - 1]); });
    }
    private void ResizeMessages() { foreach (Control control in transcript.Controls) if (control is Panel panel) panel.Width = Math.Max(400, transcript.ClientSize.Width - 90); }

    private static Button MakeButton(string text, int height) { var b = new Button { Text = text, Height = height, FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text, BackColor = Theme.Sidebar, Font = Theme.Ui, Cursor = Cursors.Hand }; b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Theme.SurfaceHover; return b; }
    private static void StyleButton(Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Theme.Border; b.FlatAppearance.MouseOverBackColor = Theme.SurfaceHover; b.BackColor = Theme.Surface; b.ForeColor = Theme.Text; b.Cursor = Cursors.Hand; b.Font = Theme.Ui; }
    private static void StyleAccentButton(Button b) { StyleButton(b); b.BackColor = Theme.Accent; b.FlatAppearance.BorderColor = Theme.Accent; b.ForeColor = Color.White; }
    private static void StyleCombo(ComboBox c) { c.BackColor = Theme.Surface; c.ForeColor = Theme.Text; c.FlatStyle = FlatStyle.Flat; c.Font = Theme.Ui; }
}
