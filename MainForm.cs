using System.Text.Json;

namespace PiGUI;

internal sealed class MainForm : Form
{
    private static readonly (string Label, string Id)[] CodexModels =
    {
        ("GPT-5.5", "gpt-5.5"),
        ("GPT-5.4", "gpt-5.4"),
        ("GPT-5.4 mini", "gpt-5.4-mini")
    };

    private static readonly (string Label, string Id)[] CopilotModels =
    {
        ("GPT-5 Mini", "gpt-5-mini"),
        ("GPT-5.3 Codex", "gpt-5.3-codex"),
        ("GPT-5.4", "gpt-5.4"),
        ("GPT-5.4 mini", "gpt-5.4-mini"),
        ("GPT-5.4 nano", "gpt-5.4-nano"),
        ("GPT-5.5", "gpt-5.5"),
        ("Claude Haiku 4.5", "claude-haiku-4.5"),
        ("Claude Opus 4.8", "claude-opus-4.8"),
        ("Claude Fable 5", "claude-fable-5"),
        ("Claude Sonnet 4.6", "claude-sonnet-4.6"),
        ("Gemini 3.1 Pro", "gemini-3.1-pro-preview"),
        ("Gemini 3.5 Flash", "gemini-3.5-flash")
    };

    private readonly AppSettings settings = AppSettings.Load();
    private readonly PiRpcClient rpc = new();
    private readonly FlowLayoutPanel transcript = new() { Tag = "background" };
    private readonly FlowLayoutPanel attachmentBar = new() { Tag = "surface" };
    private readonly FlowLayoutPanel recentProjects = new() { Tag = "sidebar" };
    private readonly ComposerBox composer = new() { Tag = "composer" };
    private readonly ModernDropdown providerBox = new();
    private readonly ModernDropdown modelBox = new();
    private readonly ModernDropdown effortBox = new();
    private readonly ModernDropdown approvalBox = new();
    private readonly ModernButton projectButton = new() { Tag = "surface" };
    private readonly ModernButton sendButton = new() { Tag = "accent" };
    private readonly ModernButton stopButton = new() { Tag = "surface" };
    private readonly ModernButton themeButton = new() { Tag = "sidebar" };
    private readonly RoundedPanel composerCard = new() { Tag = "surface" };
    private ModernButton? attachButton;
    private readonly Label statusLabel = new() { Tag = "muted" };
    private readonly Label authLabel = new() { Tag = "muted" };
    private readonly Label usageFooterLabel = new() { Tag = "muted" };
    private readonly List<Attachment> attachments = new();
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly ResponseUsageTracker responseUsage;
    private MarkdownRichTextBox? streamingMessage;
    private Label? streamingMetadata;
    private long sessionTotalTokens;
    private double sessionTotalCredits;
    private long? contextTokens;
    private long? contextWindow;
    private double? contextPercent;
    private bool initialized;
    private bool updatingSelections;

    public MainForm()
    {
        responseUsage = new ResponseUsageTracker(FriendlyModelName);
        Theme.SetMode(settings.ThemeMode);
        Text = "Pi GUI for Windows";
        MinimumSize = new Size(1020, 680);
        Size = new Size(1240, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Ui;
        AllowDrop = true;
        BuildLayout();
        WireEvents();
        PopulateSettings();
        ApplyThemeTree(this);
        Shown += (_, _) => RunUiActionAsync(async () => { PositionComposerControls(); await ConnectAsync(); }, "start Pi");
        FormClosing += (_, _) => { settings.Save(); rpc.DisposeAsync().AsTask().GetAwaiter().GetResult(); };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Tag = "background" };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), Tag = "sidebar" };
        root.Controls.Add(sidebar, 0, 0);
        sidebar.Controls.Add(new Label { Text = "π  Pi for Windows", AutoSize = true, Font = new Font("Segoe UI Semibold", 13), Location = new Point(19, 20), Tag = "text" });
        themeButton.Text = Theme.IsDark ? "☀" : "☾"; themeButton.Font = new Font("Segoe UI Symbol", 12); themeButton.Location = new Point(201, 13); themeButton.Size = new Size(34, 34); themeButton.DrawBorder = false;
        themeButton.Click += (_, _) => ToggleTheme(); sidebar.Controls.Add(themeButton);

        var newChat = MakeButton("＋   New thread", 42, "sidebar");
        newChat.Location = new Point(14, 66); newChat.Width = 220; newChat.TextAlign = ContentAlignment.MiddleLeft; newChat.Padding = new Padding(12, 0, 0, 0);
        newChat.Click += (_, _) => RunUiActionAsync(NewChatAsync, "start a new thread"); sidebar.Controls.Add(newChat);
        var newProject = MakeButton("✦   Work in a new project", 38, "sidebar");
        newProject.Location = new Point(14, 114); newProject.Width = 220; newProject.TextAlign = ContentAlignment.MiddleLeft; newProject.Padding = new Padding(12, 0, 0, 0);
        newProject.Click += (_, _) => RunUiAction(CreateNewProject, "create a project"); sidebar.Controls.Add(newProject);
        sidebar.Controls.Add(new Label { Text = "WORKSPACES", AutoSize = true, Font = new Font("Segoe UI Semibold", 8), Location = new Point(20, 174), Tag = "muted" });
        recentProjects.Location = new Point(10, 197); recentProjects.Width = 228; recentProjects.Height = 370; recentProjects.FlowDirection = FlowDirection.TopDown; recentProjects.WrapContents = false; recentProjects.AutoScroll = true;
        sidebar.Controls.Add(recentProjects);

        var accounts = MakeButton("⚙   Accounts + settings", 40, "sidebar");
        accounts.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; accounts.Width = 220; accounts.TextAlign = ContentAlignment.MiddleLeft; accounts.Padding = new Padding(11, 0, 0, 0);
        accounts.Click += (_, _) => RunUiAction(OpenAccounts, "open account settings"); sidebar.Controls.Add(accounts);
        authLabel.AutoSize = true; authLabel.Font = Theme.Small; sidebar.Controls.Add(authLabel);
        void PositionSidebarFooter() { accounts.Top = sidebar.ClientSize.Height - 58; authLabel.Location = new Point(20, accounts.Top - 30); }
        sidebar.Resize += (_, _) => PositionSidebarFooter(); PositionSidebarFooter();

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Tag = "background" };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 215));
        root.Controls.Add(main, 1, 0);

        var toolbar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 13, 24, 8), Tag = "background" };
        main.Controls.Add(toolbar, 0, 0);
        projectButton.TextAlign = ContentAlignment.MiddleLeft; projectButton.AutoEllipsis = true; projectButton.Padding = new Padding(12, 0, 0, 0); projectButton.Location = new Point(24, 13); projectButton.Size = new Size(305, 40);
        toolbar.Controls.Add(projectButton);
        statusLabel.AutoSize = true; statusLabel.Location = new Point(348, 25); toolbar.Controls.Add(statusLabel);

        transcript.Dock = DockStyle.Fill; transcript.AutoScroll = true; transcript.FlowDirection = FlowDirection.TopDown; transcript.WrapContents = false; transcript.Padding = new Padding(56, 24, 56, 24);
        main.Controls.Add(transcript, 0, 1);

        var composerHost = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(50, 10, 50, 7), ColumnCount = 1, RowCount = 2, Tag = "background" };
        composerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composerHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        composerHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        main.Controls.Add(composerHost, 0, 2);
        composerCard.Dock = DockStyle.Fill; composerCard.Margin = Padding.Empty; composerCard.Padding = new Padding(14); composerCard.Radius = 16; composerCard.BorderWidth = 1;
        composerHost.Controls.Add(composerCard, 0, 0);
        attachmentBar.Dock = DockStyle.Top; attachmentBar.Height = 34; attachmentBar.WrapContents = false; attachmentBar.AutoScroll = true; composerCard.Controls.Add(attachmentBar);
        composer.BorderStyle = BorderStyle.None; composer.Font = new Font("Segoe UI", 11); composer.Location = new Point(17, 43); composer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; composerCard.Controls.Add(composer);

        attachButton = MakeButton("＋", 38, "surface"); attachButton.Font = new Font("Segoe UI", 14); attachButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; attachButton.Width = 40; attachButton.Click += (_, _) => RunUiAction(ChooseFiles, "attach files"); composerCard.Controls.Add(attachButton);
        SetupCombo(providerBox, 148); SetupCombo(modelBox, 178); SetupCombo(effortBox, 90); SetupCombo(approvalBox, 144);
        composerCard.Controls.Add(providerBox); composerCard.Controls.Add(modelBox); composerCard.Controls.Add(effortBox); composerCard.Controls.Add(approvalBox);

        sendButton.Text = "↑"; sendButton.Font = new Font("Segoe UI Semibold", 15); sendButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; sendButton.Size = new Size(44, 40); sendButton.Radius = 11; composerCard.Controls.Add(sendButton);
        stopButton.Text = "■"; stopButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; stopButton.Size = new Size(44, 40); stopButton.Radius = 11; stopButton.Visible = false; composerCard.Controls.Add(stopButton);
        usageFooterLabel.Font = new Font("Segoe UI", 8F); usageFooterLabel.TextAlign = ContentAlignment.MiddleRight; usageFooterLabel.AutoEllipsis = true;
        usageFooterLabel.Dock = DockStyle.Fill; usageFooterLabel.Margin = Padding.Empty; usageFooterLabel.Tag = "transparent-muted";
        composerHost.Controls.Add(usageFooterLabel, 0, 1);
        composerCard.Resize += (_, _) => PositionComposerControls();
    }

    private void PositionComposerControls()
    {
        if (attachButton is null) return;
        var compact = composerCard.ClientSize.Width < 740;
        providerBox.Width = compact ? 132 : 148;
        modelBox.Width = compact ? 142 : 178;
        approvalBox.Width = compact ? 120 : 144;
        composer.Size = new Size(Math.Max(100, composerCard.ClientSize.Width - 34), Math.Max(45, composerCard.ClientSize.Height - 100));
        var y = Math.Max(8, composerCard.ClientSize.Height - 54); var x = 14;
        attachButton.Location = new Point(x, y); x += 48;
        providerBox.Location = new Point(x, y + 4); x += providerBox.Width + 8;
        modelBox.Location = new Point(x, y + 4); x += modelBox.Width + 8;
        effortBox.Location = new Point(x, y + 4); x += effortBox.Width + 8;
        approvalBox.Location = new Point(x, y + 4);
        sendButton.Location = new Point(composerCard.ClientSize.Width - 58, y); stopButton.Location = sendButton.Location;
    }

    private static void SetupCombo(ModernDropdown combo, int width) { combo.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; combo.Size = new Size(width, 32); combo.Tag = "surface"; }

    private void WireEvents()
    {
        rpc.EventReceived += e => Ui(() => HandleEvent(e));
        rpc.ErrorReceived += text => Ui(() => ShowRuntimeError(text));
        rpc.Exited += () => Ui(() => SetStatus("Disconnected", false));
        projectButton.Click += (_, _) => RunUiAction(ChooseProject, "choose a project");
        providerBox.SelectedIndexChanged += (_, _) => RunSelectionAction(ChangeProviderAsync, "change provider");
        modelBox.SelectedIndexChanged += (_, _) => RunSelectionAction(ChangeModelAsync, "change model");
        effortBox.SelectedIndexChanged += (_, _) => RunSelectionAction(ChangeEffortAsync, "change reasoning effort");
        approvalBox.SelectedIndexChanged += (_, _) => RunSelectionAction(ChangeApprovalAsync, "change approval mode");
        sendButton.Click += (_, _) => RunUiActionAsync(SendAsync, "send the message");
        stopButton.Click += (_, _) => RunUiActionAsync(async () => { if (rpc.IsRunning) await rpc.SendAsync(new { type = "abort" }); }, "stop the response");
        composer.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; RunUiActionAsync(SendAsync, "send the message"); } };
        composer.ImagePasted += AddClipboardImage;
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) AddFiles(files); };
        transcript.Resize += (_, _) => ResizeMessages();
    }

    private void PopulateSettings()
    {
        providerBox.Items.AddRange(new object[] { "Codex", "GitHub Copilot" });
        effortBox.Items.AddRange(new object[] { "low", "medium", "high", "xhigh" });
        approvalBox.Items.AddRange(new object[] { "Ask for approval", "Approve for me", "Full access", "Custom" });
        approvalBox.Descriptions["Ask for approval"] = "Confirm every action that changes your project";
        approvalBox.Descriptions["Approve for me"] = "Only interrupt for potentially unsafe actions";
        approvalBox.Descriptions["Full access"] = "Allow tools without confirmation";
        approvalBox.Descriptions["Custom"] = "Use a conservative custom policy";
        providerBox.SelectedItem = settings.Provider == "github-copilot" ? "GitHub Copilot" : "Codex";
        PopulateModels(settings.Model); effortBox.SelectedItem = settings.Effort; approvalBox.SelectedItem = ApprovalLabel(settings.ApprovalMode);
        if (!Directory.Exists(settings.ProjectPath)) settings.ProjectPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        UpdateProjectLabel(); RefreshRecentProjects(); RefreshAuthStatus(); AddWelcome(); UpdateUsageFooter(); initialized = true;
    }

    private async Task ConnectAsync()
    {
        await connectionLock.WaitAsync();
        try
        {
            SetStatus("Starting pi…", false);
            await rpc.StartAsync(settings.ProjectPath, ProviderId(), ModelId(), settings.Effort, settings.ApprovalMode);
            var connected = OAuthService.IsConnected(ProviderId());
            SetStatus(connected ? "Ready" : $"{providerBox.SelectedItem} sign-in required", connected);
        }
        catch (Exception ex) { SetStatus("Setup needed", false); AddSystemMessage(ex.Message + "\n\nOpen Accounts & settings if this provider is not connected yet.", true); }
        finally { connectionLock.Release(); }
        RefreshAuthStatus();
        await RefreshSessionStatsAsync();
    }

    private async Task SendAsync()
    {
        var text = composer.Text.Trim(); if (text.Length == 0 && attachments.Count == 0) return;
        if (!rpc.IsRunning) { await ConnectAsync(); if (!rpc.IsRunning) return; }
        var outgoing = attachments.ToList();
        await connectionLock.WaitAsync();
        try
        {
            if (!rpc.IsRunning) throw new InvalidOperationException("Pi is reconnecting. Please send the message again in a moment.");
            await rpc.PromptAsync(text, outgoing);
            AddUserMessage(text.Length == 0 ? "[Attachments]" : text, outgoing);
            composer.Clear(); attachments.Clear(); RefreshAttachments();
        }
        catch (Exception ex) { AddSystemMessage(ex.Message, true); SetBusy(false); }
        finally { connectionLock.Release(); }
    }

    private void HandleEvent(JsonElement e)
    {
        if (!e.TryGetProperty("type", out var typeNode)) return;
        switch (typeNode.GetString())
        {
            case "agent_start":
                SetBusy(true); responseUsage.Begin(ProviderId(), ModelId()); EnsureStreamingMessage(); break;
            case "agent_end":
                SetBusy(false); FinishStreamingMessage();
                RunUiActionAsync(RefreshSessionStatsAsync, "refresh usage");
                break;
            case "message_end":
                if (e.TryGetProperty("message", out var completedMessage)) responseUsage.AddMessage(completedMessage);
                break;
            case "extension_ui_request": _ = HandleExtensionRequestAsync(e); break;
            case "message_update":
                if (e.TryGetProperty("assistantMessageEvent", out var update) && update.TryGetProperty("type", out var updateType))
                {
                    if (updateType.GetString() == "text_delta" && update.TryGetProperty("delta", out var delta)) AppendStream(delta.GetString() ?? "");
                    else if (updateType.GetString() == "thinking_delta") statusLabel.Text = "Thinking…";
                    else if (updateType.GetString() == "error" && update.TryGetProperty("error", out var error)) AddSystemMessage(error.ToString(), true);
                }
                break;
            case "tool_execution_start": AddToolMessage($"Running {(e.TryGetProperty("toolName", out var tn) ? tn.GetString() : "tool")}…"); break;
            case "tool_execution_end":
                var failed = e.TryGetProperty("isError", out var ie) && ie.GetBoolean();
                AddToolMessage($"{(e.TryGetProperty("toolName", out var en) ? en.GetString() : "tool")} {(failed ? "failed" : "finished")}"); break;
            case "auto_retry_start": statusLabel.Text = "Retrying…"; break;
            case "compaction_start": statusLabel.Text = "Compacting context…"; break;
        }
    }

    private async Task HandleExtensionRequestAsync(JsonElement e)
    {
        if (!e.TryGetProperty("method", out var method) || method.GetString() != "confirm") return;
        var id = e.GetProperty("id").GetString()!;
        var title = e.TryGetProperty("title", out var t) ? t.GetString() ?? "Approve action?" : "Approve action?";
        var detail = e.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        using var dialog = new ApprovalForm(title, detail);
        dialog.ShowDialog(this);
        await rpc.SendRawAsync(new { type = "extension_ui_response", id, confirmed = dialog.Approved });
    }

    private void EnsureStreamingMessage()
    {
        if (streamingMessage is not null) return;
        streamingMessage = CreateMessageBox("", false);
        transcript.Controls.Add(WrapMessage("PI", streamingMessage, false, true));
        ScrollToBottom();
    }

    private void AppendStream(string text)
    {
        EnsureStreamingMessage(); streamingMessage!.AppendMarkdown(text); ResizeMessageBox(streamingMessage); ScrollToBottom();
    }

    private void FinishStreamingMessage()
    {
        if (streamingMessage is { MarkdownLength: 0 }) streamingMessage.SetMarkdown("Done.");
        if (streamingMetadata is not null) streamingMetadata.Text = responseUsage.Finish();
        sessionTotalTokens += responseUsage.TotalTokens;
        sessionTotalCredits += responseUsage.EstimatedCopilotCredits;
        if (streamingMessage is not null) ResizeMessageBox(streamingMessage);
        streamingMessage = null; streamingMetadata = null; statusLabel.Text = "Ready"; UpdateUsageFooter();
    }
    private void AddWelcome() => AddSystemMessage("What would you like to build?\n\nPi can read and edit files, run commands, and work across the selected project. Paste an image, drop files here, or attach them below.", false);

    private void AddUserMessage(string text, List<Attachment> files)
    {
        var details = files.Count == 0 ? "" : "\n\n" + string.Join("  •  ", files.Select(f => f.Name));
        transcript.Controls.Add(WrapMessage("YOU", CreateMessageBox(text + details, true), true)); ScrollToBottom();
    }

    private void AddSystemMessage(string text, bool error)
    {
        var box = CreateMessageBox(text, false, error ? Color.FromArgb(225, 92, 92) : null);
        transcript.Controls.Add(WrapMessage(error ? "NOTICE" : "PI", box, false)); ScrollToBottom();
    }

    private void AddToolMessage(string text)
    {
        var chip = new ModernButton { Text = "⚙  " + text, AutoSize = true, Height = 30, Margin = new Padding(12, 4, 0, 5), Font = Theme.Small, Tag = "surface", Enabled = false };
        transcript.Controls.Add(chip); ApplyThemeTree(chip); ScrollToBottom();
    }

    private Control WrapMessage(string author, RichTextBox box, bool user, bool showResponseMetadata = false)
    {
        var host = new Panel { Width = Math.Max(420, transcript.ClientSize.Width - 115), Height = box.Height + 48, Margin = new Padding(0, 6, 0, 12), Tag = user ? "message-user" : "message-assistant" };
        var bubbleWidth = Math.Min(760, Math.Max(360, host.Width - 120));
        var bubble = new RoundedPanel { Width = bubbleWidth, Height = box.Height + 30, Radius = 13, BorderWidth = 1, Tag = user ? "bubble-user" : "bubble-assistant" };
        bubble.Left = user ? host.Width - bubble.Width - 4 : 4; bubble.Top = 8; bubble.Anchor = user ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left;
        var name = new Label { Text = author, AutoSize = true, Font = new Font("Segoe UI Semibold", 8), Location = new Point(14, 8), Tag = "muted" };
        box.Location = new Point(12, 27); box.Width = bubble.Width - 24; box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bubble.Controls.Add(name); bubble.Controls.Add(box);
        if (showResponseMetadata)
        {
            streamingMetadata = new Label { Text = "Generating…", AutoEllipsis = true, Font = new Font("Segoe UI", 8F), Height = 18, Width = bubble.Width - 24, Tag = "response-meta" };
            bubble.Controls.Add(streamingMetadata);
        }
        host.Controls.Add(bubble); ApplyThemeTree(host); ResizeMessageBox(box); return host;
    }

    private MarkdownRichTextBox CreateMessageBox(string text, bool user, Color? textColor = null)
    {
        var box = new MarkdownRichTextBox { Width = 700, Tag = user ? "bubble-user" : "bubble-assistant" };
        box.SetMarkdown(text, textColor);
        ResizeMessageBox(box); return box;
    }

    private static void ResizeMessageBox(RichTextBox box)
    {
        var measured = TextRenderer.MeasureText(box.Text + "\n", box.Font, new Size(Math.Max(200, box.Width - 12), int.MaxValue), TextFormatFlags.WordBreak);
        box.Height = Math.Min(700, Math.Max(38, measured.Height + 10));
        if (box.Parent is RoundedPanel bubble)
        {
            var metadata = bubble.Controls.OfType<Label>().FirstOrDefault(label => Equals(label.Tag, "response-meta"));
            if (metadata is not null) { metadata.Location = new Point(12, box.Bottom + 5); metadata.Width = bubble.Width - 24; }
            bubble.Height = box.Height + (metadata is null ? 38 : 61);
            if (bubble.Parent is Panel host) host.Height = bubble.Height + 16;
        }
    }

    private async Task NewChatAsync() { try { if (rpc.IsRunning) await rpc.SendAsync(new { type = "new_session" }); } catch { } transcript.Controls.Clear(); streamingMessage = null; streamingMetadata = null; ResetChatUsage(); AddWelcome(); }

    private void ChooseProject()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the workspace Pi should work in", SelectedPath = settings.ProjectPath, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            RunUiActionAsync(() => SwitchProjectAsync(dialog.SelectedPath), "switch projects");
    }

    private void CreateNewProject()
    {
        using var dialog = new NewProjectForm();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.ProjectPath is { } path)
            RunUiActionAsync(() => SwitchProjectAsync(path), "switch projects");
    }

    private Task SwitchProjectAsync(string path)
    {
        settings.RememberProject(path); UpdateProjectLabel(); RefreshRecentProjects(); transcript.Controls.Clear(); ResetChatUsage(); AddWelcome(); return ConnectAsync();
    }

    private void UpdateProjectLabel() => projectButton.Text = $"📁  {DisplayFolder(settings.ProjectPath)}    ▾";
    private static string DisplayFolder(string path) => Path.GetFileName(path) is { Length: > 0 } name ? name : path;

    private void RefreshRecentProjects()
    {
        recentProjects.Controls.Clear();
        foreach (var path in settings.RecentProjects.Prepend(settings.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var button = MakeButton("📁  " + DisplayFolder(path), 37, "sidebar"); button.Width = 214; button.TextAlign = ContentAlignment.MiddleLeft; button.Padding = new Padding(10, 0, 0, 0); button.Tag = path; button.AutoEllipsis = true;
            button.Click += (_, _) => RunUiActionAsync(() => SwitchProjectAsync((string)button.Tag), "switch projects"); recentProjects.Controls.Add(button);
        }
        ApplyThemeTree(recentProjects);
    }

    private void ChooseFiles() { using var dialog = new OpenFileDialog { Multiselect = true, Title = "Attach images or files", Filter = "All files|*.*" }; if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames); }
    private void AddFiles(IEnumerable<string> files) { foreach (var file in files.Where(File.Exists)) { var image = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(Path.GetExtension(file).ToLowerInvariant()); if (!attachments.Any(a => string.Equals(a.Path, file, StringComparison.OrdinalIgnoreCase))) attachments.Add(new Attachment(file, image)); } RefreshAttachments(); }
    private void AddClipboardImage(Image image) { var dir = Path.Combine(Path.GetTempPath(), "PiGUI", "attachments"); Directory.CreateDirectory(dir); var path = Path.Combine(dir, $"pasted-{DateTime.Now:yyyyMMdd-HHmmssfff}.png"); image.Save(path, System.Drawing.Imaging.ImageFormat.Png); image.Dispose(); AddFiles(new[] { path }); }

    private void RefreshAttachments()
    {
        attachmentBar.Controls.Clear();
        foreach (var item in attachments.ToList())
        {
            var chip = MakeButton((item.IsImage ? "▧  " : "▤  ") + item.Name + "  ×", 29, "surface"); chip.AutoSize = true; chip.Tag = item;
            chip.Click += (_, _) => { attachments.Remove((Attachment)chip.Tag); RefreshAttachments(); }; attachmentBar.Controls.Add(chip);
        }
    }

    private async Task ChangeModelAsync()
    {
        if (modelBox.SelectedItem is null) return; settings.Model = ModelId(); settings.Save(); if (!initialized) return;
        await connectionLock.WaitAsync();
        try { if (rpc.IsRunning) await rpc.SendAsync(new { type = "set_model", provider = ProviderId(), modelId = settings.Model }); }
        catch (Exception ex) { AddSystemMessage(ex.Message, true); }
        finally { connectionLock.Release(); }
    }
    private async Task ChangeProviderAsync()
    {
        if (providerBox.SelectedItem is null) return;
        ResetChatUsage();
        settings.Provider = ProviderId();
        updatingSelections = true;
        try { PopulateModels(settings.Provider == "github-copilot" ? "gpt-5.3-codex" : "gpt-5.5"); }
        finally { updatingSelections = false; }
        settings.Model = ModelId(); settings.Save(); RefreshAuthStatus();
        if (initialized) await ConnectAsync();
    }
    private async Task ChangeEffortAsync()
    {
        if (effortBox.SelectedItem is not string effort) return; settings.Effort = effort; settings.Save(); if (!initialized) return;
        await connectionLock.WaitAsync();
        try { if (rpc.IsRunning) await rpc.SendAsync(new { type = "set_thinking_level", level = effort }); }
        catch (Exception ex) { AddSystemMessage(ex.Message, true); }
        finally { connectionLock.Release(); }
    }
    private async Task ChangeApprovalAsync() { if (approvalBox.SelectedItem is null) return; settings.ApprovalMode = ApprovalId(approvalBox.SelectedItem.ToString()!); settings.Save(); if (initialized) { ResetChatUsage(); await ConnectAsync(); } }

    private void RunSelectionAction(Func<Task> action, string description)
    {
        if (!updatingSelections) RunUiActionAsync(action, description);
    }

    private async void RunUiActionAsync(Func<Task> action, string description)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ReportUiFailure(description, ex); }
    }

    private void RunUiAction(Action action, string description)
    {
        try { action(); }
        catch (Exception ex) { ReportUiFailure(description, ex); }
    }

    private void ReportUiFailure(string description, Exception ex)
    {
        SetBusy(false);
        AddSystemMessage($"Could not {description}: {ex.Message}", true);
    }

    private string ProviderId() => providerBox.SelectedItem?.ToString() == "GitHub Copilot" ? "github-copilot" : "openai-codex";
    private string ModelId()
    {
        var models = ProviderId() == "github-copilot" ? CopilotModels : CodexModels;
        var selected = modelBox.SelectedItem?.ToString();
        return models.FirstOrDefault(model => model.Label == selected).Id ?? models[0].Id;
    }

    private static string FriendlyModelName(string id) =>
        CopilotModels.Concat(CodexModels).FirstOrDefault(model => model.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Label
        ?? id;

    private void ResetChatUsage()
    {
        sessionTotalTokens = 0; sessionTotalCredits = 0;
        contextTokens = null; contextWindow = null; contextPercent = null;
        UpdateUsageFooter();
    }

    private async Task RefreshSessionStatsAsync()
    {
        if (!rpc.IsRunning) { UpdateUsageFooter(); return; }
        try
        {
            var response = await rpc.SendAsync(new { type = "get_session_stats" });
            if (!response.TryGetProperty("data", out var data)) return;
            if (data.TryGetProperty("tokens", out var tokens) && tokens.TryGetProperty("total", out var total) && total.TryGetInt64(out var tokenTotal))
                sessionTotalTokens = tokenTotal;
            if (data.TryGetProperty("contextUsage", out var context) && context.ValueKind == JsonValueKind.Object)
            {
                contextTokens = context.TryGetProperty("tokens", out var used) && used.TryGetInt64(out var usedValue) ? usedValue : null;
                contextWindow = context.TryGetProperty("contextWindow", out var window) && window.TryGetInt64(out var windowValue) ? windowValue : null;
                contextPercent = context.TryGetProperty("percent", out var percent) && percent.TryGetDouble(out var percentValue) ? percentValue : null;
            }
            UpdateUsageFooter();
        }
        catch { UpdateUsageFooter(); }
    }

    private void UpdateUsageFooter()
    {
        var context = contextPercent is not null
            ? $"Context {contextPercent:0.#}%  ({FormatCompact(contextTokens)} / {FormatCompact(contextWindow)})"
            : "Context —";
        var total = ProviderId() == "github-copilot"
            ? $"Est. credits {sessionTotalCredits:0.##}"
            : $"Total tokens {ResponseUsageTracker.FormatNumber(sessionTotalTokens)}";
        usageFooterLabel.Text = $"{context}     ·     {total}";
    }

    private static string FormatCompact(long? value)
    {
        if (value is null) return "—";
        if (value >= 1_000_000) return $"{value / 1_000_000D:0.#}M";
        if (value >= 1_000) return $"{value / 1_000D:0.#}K";
        return value.Value.ToString("N0");
    }
    private static string ApprovalId(string label) => label switch { "Approve for me" => "auto", "Full access" => "full", "Custom" => "custom", _ => "ask" };
    private static string ApprovalLabel(string id) => id switch { "auto" => "Approve for me", "full" => "Full access", "custom" => "Custom", _ => "Ask for approval" };

    private void PopulateModels(string preferredId)
    {
        var models = ProviderId() == "github-copilot" ? CopilotModels : CodexModels;
        modelBox.Items.Clear();
        modelBox.Items.AddRange(models.Select(model => (object)model.Label));
        var preferred = models.FirstOrDefault(model => model.Id == preferredId).Label;
        modelBox.SelectedItem = preferred is not null ? preferred : models[0].Label;
    }

    private void OpenAccounts() { using var dialog = new AccountsForm(); dialog.AccountsChanged += async () => { RefreshAuthStatus(); ResetChatUsage(); await ConnectAsync(); }; dialog.ShowDialog(this); RefreshAuthStatus(); }
    private void RefreshAuthStatus() { var codex = OAuthService.IsConnected("openai-codex"); var copilot = OAuthService.IsConnected("github-copilot"); authLabel.Text = $"{(codex ? "●" : "○")} Codex   {(copilot ? "●" : "○")} Copilot"; authLabel.ForeColor = codex || copilot ? Theme.Success : Theme.Muted; }
    private void SetBusy(bool value) { sendButton.Visible = !value; stopButton.Visible = value; providerBox.Enabled = !value; modelBox.Enabled = !value; effortBox.Enabled = !value; approvalBox.Enabled = !value; statusLabel.Text = value ? "Working…" : "Ready"; }
    private void SetStatus(string text, bool connected) { statusLabel.Text = (connected ? "●  " : "○  ") + text; statusLabel.ForeColor = connected ? Theme.Success : Theme.Muted; }
    private void ShowRuntimeError(string text) { if (text.Contains("error", StringComparison.OrdinalIgnoreCase) || text.Contains("No API key", StringComparison.OrdinalIgnoreCase)) statusLabel.Text = "Runtime notice"; }
    private void Ui(Action action) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(action); else action(); }
    private void ScrollToBottom() { if (!IsHandleCreated) return; BeginInvoke(() => { if (!IsDisposed && transcript.Controls.Count > 0) transcript.ScrollControlIntoView(transcript.Controls[transcript.Controls.Count - 1]); }); }

    private void ResizeMessages()
    {
        foreach (Control control in transcript.Controls)
        {
            if (control is not Panel host || host.Tag?.ToString()?.StartsWith("message-") != true) continue;
            host.Width = Math.Max(420, transcript.ClientSize.Width - 115);
            if (host.Controls.OfType<RoundedPanel>().FirstOrDefault() is { } bubble && host.Tag?.ToString() == "message-user") bubble.Left = host.Width - bubble.Width - 4;
        }
    }

    private void ToggleTheme()
    {
        settings.ThemeMode = Theme.IsDark ? "light" : "dark"; Theme.SetMode(settings.ThemeMode); settings.Save(); themeButton.Text = Theme.IsDark ? "☀" : "☾"; ApplyThemeTree(this); Invalidate(true);
    }

    private static void ApplyThemeTree(Control control)
    {
        var tag = control.Tag?.ToString();
        control.ForeColor = tag is "muted" or "response-meta" or "transparent-muted" ? Theme.Muted : Theme.Text;
        control.BackColor = tag switch
        {
            "sidebar" => Theme.Sidebar, "surface" or "composer" => Theme.Surface,
            "bubble-assistant" => Theme.AssistantBubble, "bubble-user" => Theme.UserBubble, "accent" => Theme.Accent,
            "transparent-muted" => Color.Transparent,
            "muted" or "response-meta" => control.Parent?.BackColor ?? Theme.Background,
            _ => Theme.Background
        };
        if (control is ModernButton button)
        {
            button.NormalColor = tag == "accent" ? Theme.Accent : tag == "sidebar" ? Theme.Sidebar : Theme.Surface;
            button.HoverColor = tag == "accent" ? Theme.AccentHover : Theme.SurfaceHover; button.BorderColor = tag == "sidebar" ? Theme.Sidebar : Theme.Border; button.ForeColor = tag == "accent" ? Color.White : Theme.Text; button.Invalidate();
        }
        if (control is ModernDropdown dropdown) dropdown.Invalidate();
        if (control is MarkdownRichTextBox markdown) markdown.ApplyMarkdownTheme();
        if (control is RoundedPanel rounded) { rounded.BorderColor = Theme.Border; rounded.Invalidate(); }
        foreach (Control child in control.Controls) ApplyThemeTree(child);
    }

    private static ModernButton MakeButton(string text, int height, string tag)
        => new() { Text = text, Height = height, ForeColor = Theme.Text, Font = Theme.Ui, Tag = tag, Radius = 9, NormalColor = tag == "sidebar" ? Theme.Sidebar : Theme.Surface, HoverColor = Theme.SurfaceHover, BorderColor = tag == "sidebar" ? Theme.Sidebar : Theme.Border, DrawBorder = tag != "sidebar" };
}
