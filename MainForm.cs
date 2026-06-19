using System.Text;
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
    private readonly ScrollbarlessFlowLayoutPanel transcript = new() { Tag = "background", DrawScrollIndicator = true };
    private readonly FlowLayoutPanel attachmentBar = new() { Tag = "surface" };
    private readonly ScrollbarlessFlowLayoutPanel recentProjects = new() { Tag = "sidebar" };
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
    private readonly TableLayoutPanel mainLayout = new() { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Tag = "background" };
    private readonly AccountsPanel accountsPanel = new() { Visible = false };
    private readonly TerminalPanel terminalPanel = new() { Visible = false };
    private readonly ModernButton terminalButton = new() { Tag = "surface" };
    private ModernButton? attachButton;
    private readonly Label statusLabel = new() { Tag = "muted" };
    private readonly Label authLabel = new() { Tag = "muted" };
    private readonly Label usageFooterLabel = new() { Tag = "muted" };
    private readonly List<Attachment> attachments = new();
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly ResponseUsageTracker responseUsage;
    private readonly StringBuilder pendingStreamText = new();
    private readonly System.Windows.Forms.Timer streamFlushTimer = new() { Interval = 32 };
    private readonly System.Windows.Forms.Timer streamPulseTimer = new() { Interval = 380 };
    private MarkdownRichTextBox? streamingMessage;
    private ActivityTimelinePanel? streamingActivity;
    private Panel? streamingActivityHost;
    private MarkdownRichTextBox? lastResponseSegment;
    private Label? streamingCursor;
    private long sessionTotalTokens;
    private double sessionTotalCredits;
    private long? contextTokens;
    private long? contextWindow;
    private double? contextPercent;
    private bool initialized;
    private bool updatingSelections;
    private bool scrollPending;
    private bool transcriptFollowTail = true;
    private string? currentSessionPath;

    public MainForm()
    {
        responseUsage = new ResponseUsageTracker(FriendlyModelName);
        streamFlushTimer.Tick += (_, _) => FlushStreamText();
        streamPulseTimer.Tick += (_, _) => { if (streamingCursor is { IsDisposed: false } cursor) { cursor.ForeColor = cursor.ForeColor == Theme.Accent ? Theme.Muted : Theme.Accent; cursor.Invalidate(); } };
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
        HandleCreated += (_, _) => NativeTheme.Apply(this);
        Shown += (_, _) => RunUiActionAsync(async () => { PositionComposerControls(); await ConnectAsync(); }, "start Pi");
        FormClosing += (_, _) => { streamFlushTimer.Stop(); streamFlushTimer.Dispose(); streamPulseTimer.Stop(); streamPulseTimer.Dispose(); terminalPanel.Stop(); settings.Save(); rpc.DisposeAsync().AsTask().GetAwaiter().GetResult(); };
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
        recentProjects.Location = new Point(10, 197); recentProjects.Width = 228; recentProjects.Height = 370; recentProjects.FlowDirection = FlowDirection.TopDown; recentProjects.WrapContents = false;
        sidebar.Controls.Add(recentProjects);

        var accounts = MakeButton("⚙   Accounts + settings", 40, "sidebar");
        accounts.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; accounts.Width = 220; accounts.TextAlign = ContentAlignment.MiddleLeft; accounts.Padding = new Padding(11, 0, 0, 0);
        accounts.Click += (_, _) => RunUiAction(OpenAccounts, "open account settings"); sidebar.Controls.Add(accounts);
        authLabel.AutoSize = true; authLabel.Font = Theme.Small; sidebar.Controls.Add(authLabel);
        void PositionSidebarFooter()
        {
            accounts.Top = sidebar.ClientSize.Height - 58; authLabel.Location = new Point(20, accounts.Top - 30);
            recentProjects.Height = Math.Max(100, authLabel.Top - recentProjects.Top - 12);
        }
        sidebar.Resize += (_, _) => PositionSidebarFooter(); PositionSidebarFooter();

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184)); mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        var mainHost = new Panel { Dock = DockStyle.Fill, Tag = "background" };
        root.Controls.Add(mainHost, 1, 0); mainHost.Controls.Add(mainLayout); mainHost.Controls.Add(accountsPanel); accountsPanel.BringToFront();
        accountsPanel.CloseRequested += CloseAccounts;
        accountsPanel.AccountsChanged += () => RunUiActionAsync(async () => { RefreshAuthStatus(); ResetChatUsage(); await ConnectAsync(); }, "refresh accounts");

        var toolbar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 13, 24, 8), Tag = "background" };
        mainLayout.Controls.Add(toolbar, 0, 0);
        projectButton.TextAlign = ContentAlignment.MiddleLeft; projectButton.AutoEllipsis = true; projectButton.Padding = new Padding(12, 0, 0, 0); projectButton.Location = new Point(24, 13); projectButton.Size = new Size(305, 40);
        toolbar.Controls.Add(projectButton);
        statusLabel.AutoSize = true; statusLabel.Location = new Point(348, 25); toolbar.Controls.Add(statusLabel);
        terminalButton.Text = ">_  Terminal"; terminalButton.Size = new Size(112, 36); terminalButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        terminalButton.Click += (_, _) => ToggleTerminal(); toolbar.Controls.Add(terminalButton);
        void PositionTerminalButton() => terminalButton.Location = new Point(Math.Max(360, toolbar.ClientSize.Width - 138), 15);
        toolbar.Resize += (_, _) => PositionTerminalButton(); toolbar.Layout += (_, _) => PositionTerminalButton(); PositionTerminalButton();

        transcript.Dock = DockStyle.Fill; transcript.AutoScroll = true; transcript.FlowDirection = FlowDirection.TopDown; transcript.WrapContents = false; transcript.Padding = new Padding(56, 24, 56, 48);
        mainLayout.Controls.Add(transcript, 0, 1);

        var composerHost = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(50, 10, 50, 7), ColumnCount = 1, RowCount = 2, Tag = "background" };
        composerHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composerHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        composerHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        mainLayout.Controls.Add(composerHost, 0, 2);
        composerCard.Dock = DockStyle.Fill; composerCard.Margin = Padding.Empty; composerCard.Padding = new Padding(14); composerCard.Radius = 16; composerCard.BorderWidth = 1;
        composerHost.Controls.Add(composerCard, 0, 0);
        attachmentBar.Dock = DockStyle.Top; attachmentBar.Height = 34; attachmentBar.WrapContents = false; attachmentBar.AutoScroll = true; composerCard.Controls.Add(attachmentBar);
        composer.BorderStyle = BorderStyle.None; composer.Font = new Font("Segoe UI", 11); composer.Location = new Point(17, 43); composer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; composerCard.Controls.Add(composer);
        composer.Enter += (_, _) => { composerCard.BorderColor = Theme.Accent; composerCard.BorderWidth = 1; composerCard.Invalidate(); };
        composer.Leave += (_, _) => { composerCard.BorderColor = Theme.Border; composerCard.Invalidate(); };

        attachButton = MakeButton("＋", 38, "surface"); attachButton.Font = new Font("Segoe UI", 14); attachButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; attachButton.Width = 40; attachButton.Click += (_, _) => RunUiAction(ChooseFiles, "attach files"); composerCard.Controls.Add(attachButton);
        SetupCombo(providerBox, 148); SetupCombo(modelBox, 178); SetupCombo(effortBox, 90); SetupCombo(approvalBox, 144);
        composerCard.Controls.Add(providerBox); composerCard.Controls.Add(modelBox); composerCard.Controls.Add(effortBox); composerCard.Controls.Add(approvalBox);

        sendButton.Text = "↑"; sendButton.Font = new Font("Segoe UI Semibold", 15); sendButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; sendButton.Size = new Size(44, 40); sendButton.Radius = 11; composerCard.Controls.Add(sendButton);
        stopButton.Text = "■"; stopButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom; stopButton.Size = new Size(44, 40); stopButton.Radius = 11; stopButton.Visible = false; composerCard.Controls.Add(stopButton);
        usageFooterLabel.Font = new Font("Segoe UI", 8F); usageFooterLabel.TextAlign = ContentAlignment.MiddleRight; usageFooterLabel.AutoEllipsis = true;
        usageFooterLabel.Dock = DockStyle.Fill; usageFooterLabel.Margin = Padding.Empty; usageFooterLabel.Tag = "transparent-muted";
        composerHost.Controls.Add(usageFooterLabel, 0, 1);
        composerCard.Resize += (_, _) => PositionComposerControls();
        mainLayout.Controls.Add(terminalPanel, 0, 3);
        terminalPanel.CloseRequested += CloseTerminal;
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
        transcript.Scroll += (_, _) => transcriptFollowTail = IsTranscriptNearBottom();
        transcript.MouseWheel += (_, e) => BeginInvoke(() => transcriptFollowTail = e.Delta < 0 ? IsTranscriptNearBottom() : false);
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
        var preference = settings.GetProviderPreference(ProviderId());
        settings.Model = preference.Model; settings.Effort = preference.Effort;
        PopulateModels(preference.Model); effortBox.SelectedItem = preference.Effort; approvalBox.SelectedItem = ApprovalLabel(settings.ApprovalMode);
        if (!Directory.Exists(settings.ProjectPath)) settings.ProjectPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        UpdateProjectLabel(); RefreshRecentProjects(); RefreshAuthStatus(); AddWelcome(); UpdateUsageFooter(); initialized = true;
    }

    private async Task ConnectAsync(string? sessionPath = null)
    {
        if (ProviderId() == "github-copilot" && !OAuthService.IsConnected("github-copilot"))
        {
            await rpc.StopAsync();
            SetStatus("GitHub Copilot sign-in required", false);
            RefreshAuthStatus();
            return;
        }
        await connectionLock.WaitAsync();
        try
        {
            SetStatus("Starting pi…", false);
            await rpc.StartAsync(settings.ProjectPath, ProviderId(), ModelId(), settings.Effort, settings.ApprovalMode, sessionPath);
            currentSessionPath = sessionPath ?? await GetCurrentSessionPathAsync();
            var connected = OAuthService.IsConnected(ProviderId());
            SetStatus(connected ? "Ready" : $"{providerBox.SelectedItem} sign-in required", connected);
        }
        catch (Exception ex) { SetStatus("Setup needed", false); AddSystemMessage(ex.Message + "\n\nOpen Accounts & settings if this provider is not connected yet.", true); }
        finally { connectionLock.Release(); }
        RefreshAuthStatus();
        await RefreshSessionStatsAsync();
    }

    private async Task<string?> GetCurrentSessionPathAsync()
    {
        if (!rpc.IsRunning) return null;
        var response = await rpc.SendAsync(new { type = "get_state" });
        return response.TryGetProperty("data", out var data) && data.TryGetProperty("sessionFile", out var file)
            ? file.GetString() : null;
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
                SetBusy(true); responseUsage.Begin(ProviderId(), ModelId()); lastResponseSegment = null; EnsureActivitySegment().ShowThinking(); break;
            case "agent_end":
                SetBusy(false); FinishStreamingMessage();
                RunUiActionAsync(RefreshSessionStatsAsync, "refresh usage");
                RefreshRecentProjects();
                break;
            case "message_end":
                if (e.TryGetProperty("message", out var completedMessage)) responseUsage.AddMessage(completedMessage);
                break;
            case "extension_ui_request": _ = HandleExtensionRequestAsync(e); break;
            case "message_update":
                if (e.TryGetProperty("assistantMessageEvent", out var update) && update.TryGetProperty("type", out var updateType))
                {
                    if (updateType.GetString() == "text_delta" && update.TryGetProperty("delta", out var delta)) AppendStream(delta.GetString() ?? "");
                    else if (updateType.GetString() == "thinking_delta") { statusLabel.Text = "Thinking…"; EnsureActivitySegment().ShowThinking(); }
                    else if (updateType.GetString() == "error" && update.TryGetProperty("error", out var error)) AddSystemMessage(error.ToString(), true);
                }
                break;
            case "tool_execution_start":
                FinalizeStreamingTextSegment();
                var startedTool = e.TryGetProperty("toolName", out var tn) ? tn.GetString() ?? "tool" : "tool";
                var startedId = e.TryGetProperty("toolCallId", out var startedIdNode) ? startedIdNode.GetString() : null;
                EnsureActivitySegment().StartTool(startedId ?? Guid.NewGuid().ToString("N"), startedTool, ToolInvocationSummary(e));
                break;
            case "tool_execution_end":
                var failed = e.TryGetProperty("isError", out var ie) && ie.GetBoolean();
                var endedTool = e.TryGetProperty("toolName", out var endedName) ? endedName.GetString() ?? "tool" : "tool";
                var endedId = e.TryGetProperty("toolCallId", out var endedIdNode) ? endedIdNode.GetString() ?? endedTool : endedTool;
                EnsureActivitySegment().FinishTool(endedId, endedTool, failed, failed ? ToolFailureSummary(e) : "");
                break;
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
        var approval = new InlineApprovalPanel(title, detail) { Width = Math.Min(720, Math.Max(420, transcript.ClientSize.Width - 120)) };
        transcript.Controls.Add(approval); ApplyThemeTree(approval); ScrollToBottom();
        var approved = await approval.Decision;
        transcript.Controls.Remove(approval); approval.Dispose();
        await rpc.SendRawAsync(new { type = "extension_ui_response", id, confirmed = approved });
    }

    private void EnsureStreamingMessage()
    {
        if (streamingMessage is not null) return;
        CompleteActivitySegment();
        streamingMessage = CreateMessageBox("", false);
        lastResponseSegment = streamingMessage;
        var host = WrapMessage("PI", streamingMessage, false);
        transcript.Controls.Add(host);
        if (streamingMessage.Parent is RoundedPanel bubble)
        {
            streamingCursor = new Label { Text = "▌", AutoSize = true, Font = new Font("Segoe UI Semibold", 10F), ForeColor = Theme.Accent, BackColor = bubble.BackColor, Tag = "streaming-cursor" };
            bubble.Controls.Add(streamingCursor); streamPulseTimer.Start();
        }
        ScrollToBottom();
    }

    private ActivityTimelinePanel EnsureActivitySegment()
    {
        if (streamingActivity is not null) return streamingActivity;
        var width = Math.Min(760, Math.Max(360, transcript.ClientSize.Width - 235));
        var host = new Panel
        {
            Width = Math.Max(420, transcript.ClientSize.Width - 115), Height = 46,
            Margin = new Padding(4, 2, 0, 8), Tag = "activity-host"
        };
        var activity = new ActivityTimelinePanel { Width = width, Location = new Point(4, 4), Anchor = AnchorStyles.Top | AnchorStyles.Left };
        activity.TimelineHeightChanged += () =>
        {
            host.Height = Math.Max(38, activity.Height + 8); transcript.PerformLayout();
            if (IsTranscriptNearBottom()) ScrollToBottom();
        };
        host.Controls.Add(activity); transcript.Controls.Add(host); ApplyThemeTree(host);
        streamingActivity = activity; streamingActivityHost = host; ScrollToBottom(); return activity;
    }

    private void CompleteActivitySegment()
    {
        if (streamingActivity is null) return;
        streamingActivity.Complete();
        if (streamingActivityHost is not null) streamingActivityHost.Height = Math.Max(38, streamingActivity.Height + 8);
        streamingActivity = null; streamingActivityHost = null;
    }

    private void AppendStream(string text)
    {
        EnsureStreamingMessage();
        pendingStreamText.Append(text);
        if (!streamFlushTimer.Enabled) streamFlushTimer.Start();
    }

    private void FlushStreamText(bool flushAll = false)
    {
        if (pendingStreamText.Length == 0) { streamFlushTimer.Stop(); return; }
        if (streamingMessage is null) { pendingStreamText.Clear(); streamFlushTimer.Stop(); return; }
        var keepAtBottom = IsTranscriptNearBottom();
        var count = flushAll ? pendingStreamText.Length : Math.Min(140, pendingStreamText.Length);
        var text = pendingStreamText.ToString(0, count); pendingStreamText.Remove(0, count);
        streamingMessage.AppendStreamingMarkdown(text);
        ResizeMessageBox(streamingMessage);
        if (pendingStreamText.Length == 0) streamFlushTimer.Stop();
        if (keepAtBottom) ScrollToBottom();
    }

    private void FinalizeStreamingTextSegment()
    {
        FlushStreamText(true);
        if (streamingMessage is null) return;
        if (streamingMessage.MarkdownLength == 0) streamingMessage.SetMarkdown("Done.");
        else streamingMessage.FinalizeMarkdown();
        if (streamingCursor is not null) { streamingCursor.Dispose(); streamingCursor = null; streamPulseTimer.Stop(); }
        AddCopyButton(streamingMessage);
        ResizeMessageBox(streamingMessage); streamingMessage = null; pendingStreamText.Clear(); streamFlushTimer.Stop();
    }

    private void FinishStreamingMessage()
    {
        FinalizeStreamingTextSegment(); CompleteActivitySegment();
        if (lastResponseSegment is null)
        {
            lastResponseSegment = CreateMessageBox("Done.", false);
            transcript.Controls.Add(WrapMessage("PI", lastResponseSegment, false, copyResponse: true));
        }
        AddResponseMetadata(lastResponseSegment, responseUsage.Finish());
        sessionTotalTokens += responseUsage.TotalTokens;
        sessionTotalCredits += responseUsage.EstimatedCopilotCredits;
        lastResponseSegment = null; pendingStreamText.Clear(); streamFlushTimer.Stop();
        statusLabel.Text = "Ready"; UpdateUsageFooter(); ScrollToBottom();
    }
    private void AddWelcome() => AddSystemMessage("What would you like to build?\n\nPi can read and edit files, run commands, and work across the selected project. Paste an image, drop files here, or attach them below.", false);

    private void AddUserMessage(string text, List<Attachment> files)
    {
        transcriptFollowTail = true;
        var details = files.Count == 0 ? "" : "\n\n" + string.Join("  •  ", files.Select(f => f.Name));
        transcript.Controls.Add(WrapMessage("YOU", CreateMessageBox(text + details, true), true)); ScrollToBottom();
    }

    private void AddSystemMessage(string text, bool error)
    {
        var box = CreateMessageBox(text, false, error ? Color.FromArgb(225, 92, 92) : null);
        transcript.Controls.Add(WrapMessage(error ? "NOTICE" : "PI", box, false)); ScrollToBottom();
    }

    private static string ToolFailureSummary(JsonElement e)
    {
        if (!e.TryGetProperty("result", out var result) || !result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return "";
        var text = content.EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "text" && item.TryGetProperty("text", out _))
            .Select(item => item.GetProperty("text").GetString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (text is null) return "";
        var singleLine = string.Join(" ", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return singleLine.Length <= 180 ? singleLine : singleLine[..177] + "…";
    }

    private static string ToolInvocationSummary(JsonElement e)
    {
        if (!e.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object) return "";
        foreach (var name in new[] { "command", "path", "file_path", "filePath", "query", "pattern" })
            if (args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
                return text.Length <= 150 ? text : text[..147] + "…";
        return "";
    }

    private Control WrapMessage(string author, RichTextBox box, bool user, bool showResponseMetadata = false, bool copyResponse = false)
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
            var metadata = new Label { Text = "Generating…", AutoEllipsis = true, Font = new Font("Segoe UI", 8F), Height = 18, Width = bubble.Width - 24, Tag = "response-meta" };
            bubble.Controls.Add(metadata);
        }
        if (showResponseMetadata || copyResponse)
            AddCopyButton(box);
        host.Controls.Add(bubble); ApplyThemeTree(host); ResizeMessageBox(box); return host;
    }

    private static void AddCopyButton(RichTextBox box)
    {
        if (box.Parent is not RoundedPanel bubble || bubble.Controls.OfType<ModernButton>().Any(button => Equals(button.Tag, "response-copy"))) return;
        var copy = new ModernButton { Text = "Copy", Size = new Size(44, 22), Font = new Font("Segoe UI", 8F), Radius = 6, DrawBorder = false, Tag = "response-copy" };
        copy.Click += (_, _) => CopyResponseText(box, copy); bubble.Controls.Add(copy); ApplyThemeTree(copy);
    }

    private static void AddResponseMetadata(RichTextBox box, string text)
    {
        if (box.Parent is not RoundedPanel bubble) return;
        var metadata = bubble.Controls.OfType<Label>().FirstOrDefault(label => Equals(label.Tag, "response-meta"));
        if (metadata is null)
        {
            metadata = new Label { AutoEllipsis = true, Font = new Font("Segoe UI", 8F), Height = 18, Tag = "response-meta", ForeColor = Theme.Muted, BackColor = bubble.BackColor };
            bubble.Controls.Add(metadata);
        }
        metadata.Text = text; ResizeMessageBox(box);
    }

    private static void CopyResponseText(RichTextBox box, ModernButton button)
    {
        if (string.IsNullOrWhiteSpace(box.Text)) return;
        try
        {
            Clipboard.SetText(box.Text);
            button.Text = "✓";
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); if (!button.IsDisposed) button.Text = "Copy"; };
            timer.Start();
        }
        catch { button.Text = "Retry"; }
    }

    private MarkdownRichTextBox CreateMessageBox(string text, bool user, Color? textColor = null)
    {
        var box = new MarkdownRichTextBox { Width = 700, Tag = user ? "bubble-user" : "bubble-assistant" };
        box.SetMarkdown(text, textColor);
        ResizeMessageBox(box); return box;
    }

    private static void ResizeMessageBox(RichTextBox box)
    {
        var lineHeight = TextRenderer.MeasureText("Ag", box.Font).Height;
        var contentBottom = box.TextLength == 0 ? 0 : box.GetPositionFromCharIndex(Math.Max(0, box.TextLength - 1)).Y + lineHeight;
        box.Height = Math.Min(20_000, Math.Max(26, contentBottom + 5));
        if (box.Parent is RoundedPanel bubble)
        {
            var pulse = bubble.Controls.OfType<Label>().FirstOrDefault(label => Equals(label.Tag, "streaming-cursor"));
            if (pulse is not null)
            {
                var end = box.GetPositionFromCharIndex(box.TextLength);
                pulse.Location = new Point(Math.Min(bubble.Width - pulse.Width - 12, box.Left + end.X + 1), box.Top + end.Y);
                pulse.BringToFront();
            }
            var activity = bubble.Controls.OfType<ActivityTimelinePanel>().FirstOrDefault();
            var cursor = box.Bottom + 5;
            if (activity is { Visible: true }) { activity.Location = new Point(12, cursor); activity.Width = bubble.Width - 24; cursor += activity.Height + 5; }
            var metadata = bubble.Controls.OfType<Label>().FirstOrDefault(label => Equals(label.Tag, "response-meta"));
            var copy = bubble.Controls.OfType<ModernButton>().FirstOrDefault(button => Equals(button.Tag, "response-copy"));
            if (metadata is not null)
            {
                metadata.Location = new Point(12, cursor); metadata.Width = bubble.Width - (copy is null ? 24 : 76);
                if (copy is not null) copy.Location = new Point(bubble.Width - copy.Width - 12, cursor - 2);
                cursor += Math.Max(metadata.Height, copy?.Height ?? 0);
            }
            else if (copy is not null) { copy.Location = new Point(bubble.Width - copy.Width - 12, cursor); cursor += copy.Height; }
            bubble.Height = metadata is null && copy is null ? box.Height + 38 : cursor + 10;
            if (bubble.Parent is Panel host) host.Height = bubble.Height + 16;
        }
    }

    private async Task NewChatAsync()
    {
        transcriptFollowTail = true;
        try { if (rpc.IsRunning) await rpc.SendAsync(new { type = "new_session" }); currentSessionPath = await GetCurrentSessionPathAsync(); }
        catch { currentSessionPath = null; }
        streamFlushTimer.Stop(); streamPulseTimer.Stop(); pendingStreamText.Clear(); transcript.Controls.Clear(); streamingMessage = null; streamingCursor = null; streamingActivity = null; streamingActivityHost = null; lastResponseSegment = null;
        ResetChatUsage(); AddWelcome(); RefreshRecentProjects();
    }

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

    private async Task SwitchProjectAsync(string path)
    {
        transcriptFollowTail = true;
        currentSessionPath = null; settings.RememberProject(path); UpdateProjectLabel(); RefreshRecentProjects(); transcript.Controls.Clear(); ResetChatUsage(); AddWelcome();
        if (terminalPanel.Visible) terminalPanel.Start(path);
        await ConnectAsync();
    }

    private void UpdateProjectLabel() => projectButton.Text = $"📁  {DisplayFolder(settings.ProjectPath)}    ▾";
    private static string DisplayFolder(string path) => Path.GetFileName(path) is { Length: > 0 } name ? name : path;

    private void RefreshRecentProjects()
    {
        var saved = SessionCatalog.Load();
        recentProjects.Controls.Clear();
        var projects = settings.RecentProjects.Prepend(settings.ProjectPath).Concat(saved.Select(session => session.ProjectPath)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var path in projects)
        {
            var button = MakeButton("📁  " + DisplayFolder(path), 35, "sidebar"); button.Width = 214; button.TextAlign = ContentAlignment.MiddleLeft; button.Padding = new Padding(10, 0, 0, 0); button.Tag = path; button.AutoEllipsis = true;
            button.Click += (_, _) => RunUiActionAsync(() => SwitchProjectAsync((string)button.Tag), "switch projects"); recentProjects.Controls.Add(button);
            foreach (var session in saved.Where(item => string.Equals(item.ProjectPath, path, StringComparison.OrdinalIgnoreCase)).Take(6))
            {
                var row = new ChatSessionRow(session, RelativeAge(session.UpdatedAt), string.Equals(currentSessionPath, session.FilePath, StringComparison.OrdinalIgnoreCase));
                row.OpenRequested += item => RunUiActionAsync(() => OpenSessionAsync(item), "open chat");
                row.DeleteRequested += item => RunUiActionAsync(() => DeleteSessionAsync(item), "delete chat");
                recentProjects.Controls.Add(row);
            }
        }
        ApplyThemeTree(recentProjects);
    }

    private async Task DeleteSessionAsync(SavedSession session)
    {
        if (string.Equals(currentSessionPath, session.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            if (rpc.IsRunning) await rpc.SendAsync(new { type = "new_session" });
            currentSessionPath = await GetCurrentSessionPathAsync();
            transcript.Controls.Clear(); ResetChatUsage(); AddWelcome();
        }
        File.Delete(session.FilePath);
        RefreshRecentProjects();
    }

    private async Task OpenSessionAsync(SavedSession session)
    {
        transcriptFollowTail = true;
        updatingSelections = true;
        try
        {
            settings.Provider = session.Provider;
            providerBox.SelectedItem = session.Provider == "github-copilot" ? "GitHub Copilot" : "Codex";
            PopulateModels(session.Model);
            effortBox.SelectedItem = effortBox.Items.Contains(session.Effort) ? session.Effort : "medium";
            settings.Model = ModelId(); settings.Effort = effortBox.SelectedItem?.ToString() ?? "medium";
            var preference = settings.GetProviderPreference(settings.Provider); preference.Model = settings.Model; preference.Effort = settings.Effort;
        }
        finally { updatingSelections = false; }
        settings.RememberProject(session.ProjectPath); UpdateProjectLabel(); transcript.Controls.Clear(); ResetChatUsage(); SetStatus("Opening chat…", false);
        await ConnectAsync(session.FilePath);
        currentSessionPath = session.FilePath;
        await LoadSessionMessagesAsync();
        await RefreshSessionStatsAsync();
        RefreshRecentProjects();
    }

    private async Task LoadSessionMessagesAsync()
    {
        transcriptFollowTail = true;
        var response = await rpc.SendAsync(new { type = "get_messages" }, TimeSpan.FromSeconds(30));
        if (!response.TryGetProperty("data", out var data) || !data.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array) return;
        transcript.SuspendLayout();
        try
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("role", out var roleNode)) continue;
                var text = SessionCatalog.ReadText(message);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (roleNode.GetString() == "user") transcript.Controls.Add(WrapMessage("YOU", CreateMessageBox(text, true), true));
                else if (roleNode.GetString() == "assistant") transcript.Controls.Add(WrapMessage("PI", CreateMessageBox(text, false), false, copyResponse: true));
            }
        }
        finally { transcript.ResumeLayout(true); }
        ScrollToBottom();
    }

    private static string RelativeAge(DateTime updatedAt)
    {
        var age = DateTime.Now - updatedAt;
        if (age.TotalMinutes < 1) return "now";
        if (age.TotalHours < 1) return $"{(int)age.TotalMinutes}m";
        if (age.TotalDays < 1) return $"{(int)age.TotalHours}h";
        if (age.TotalDays < 7) return $"{(int)age.TotalDays}d";
        return $"{Math.Max(1, (int)(age.TotalDays / 7))}w";
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
        if (modelBox.SelectedItem is null) return;
        settings.Model = ModelId(); settings.GetProviderPreference(ProviderId()).Model = settings.Model; settings.Save(); if (!initialized) return;
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
        var preference = settings.GetProviderPreference(settings.Provider);
        updatingSelections = true;
        try { PopulateModels(preference.Model); effortBox.SelectedItem = preference.Effort; }
        finally { updatingSelections = false; }
        settings.Model = ModelId(); settings.Effort = effortBox.SelectedItem?.ToString() ?? "medium"; settings.Save(); RefreshAuthStatus();
        if (initialized) await ConnectAsync();
    }
    private async Task ChangeEffortAsync()
    {
        if (effortBox.SelectedItem is not string effort) return; settings.Effort = effort; settings.GetProviderPreference(ProviderId()).Effort = effort; settings.Save(); if (!initialized) return;
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

    private void OpenAccounts() { accountsPanel.RefreshStatuses(); accountsPanel.Visible = true; accountsPanel.BringToFront(); ApplyThemeTree(accountsPanel); }
    private void CloseAccounts() { accountsPanel.Visible = false; mainLayout.BringToFront(); RefreshAuthStatus(); }
    private void ToggleTerminal()
    {
        if (terminalPanel.Visible) { CloseTerminal(); return; }
        terminalPanel.Visible = true; mainLayout.RowStyles[3].Height = Math.Max(190, ClientSize.Height / 3); terminalPanel.Start(settings.ProjectPath);
        terminalButton.Text = "×  Terminal"; mainLayout.PerformLayout(); ApplyThemeTree(terminalPanel);
    }
    private void CloseTerminal()
    {
        terminalPanel.Stop(); terminalPanel.Visible = false; mainLayout.RowStyles[3].Height = 0; terminalButton.Text = ">_  Terminal"; mainLayout.PerformLayout();
    }
    private void RefreshAuthStatus() { var codex = OAuthService.IsConnected("openai-codex"); var copilot = OAuthService.IsConnected("github-copilot"); authLabel.Text = $"{(codex ? "●" : "○")} Codex   {(copilot ? "●" : "○")} Copilot"; authLabel.ForeColor = codex || copilot ? Theme.Success : Theme.Muted; }
    private void SetBusy(bool value) { sendButton.Visible = !value; stopButton.Visible = value; providerBox.Enabled = !value; modelBox.Enabled = !value; effortBox.Enabled = !value; approvalBox.Enabled = !value; statusLabel.Text = value ? "Working…" : "Ready"; }
    private void SetStatus(string text, bool connected) { statusLabel.Text = (connected ? "●  " : "○  ") + text; statusLabel.ForeColor = connected ? Theme.Success : Theme.Muted; }
    private void ShowRuntimeError(string text) { if (text.Contains("error", StringComparison.OrdinalIgnoreCase) || text.Contains("No API key", StringComparison.OrdinalIgnoreCase)) statusLabel.Text = "Runtime notice"; }
    private void Ui(Action action) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke(action); else action(); }
    private bool IsTranscriptNearBottom()
    {
        var range = transcript.VerticalScroll.Maximum - transcript.VerticalScroll.LargeChange + 1;
        if (range <= 0) return true;
        return transcript.VerticalScroll.Value >= range - 80;
    }

    private void ScrollToBottom()
    {
        if (!transcriptFollowTail || !IsHandleCreated || scrollPending) return;
        scrollPending = true;
        BeginInvoke(() =>
        {
            try
            {
                if (IsDisposed || transcript.Controls.Count == 0) return;
                transcript.PerformLayout();
                transcript.ScrollControlIntoView(transcript.Controls[transcript.Controls.Count - 1]);
            }
            finally { scrollPending = false; }
        });
    }

    private void ResizeMessages()
    {
        foreach (Control control in transcript.Controls)
        {
            if (control is not Panel host) continue;
            if (Equals(host.Tag, "activity-host"))
            {
                host.Width = Math.Max(420, transcript.ClientSize.Width - 115);
                if (host.Controls.OfType<ActivityTimelinePanel>().FirstOrDefault() is { } activity)
                    activity.Width = Math.Min(760, Math.Max(360, transcript.ClientSize.Width - 235));
                continue;
            }
            if (host.Tag?.ToString()?.StartsWith("message-") != true) continue;
            host.Width = Math.Max(420, transcript.ClientSize.Width - 115);
            if (host.Controls.OfType<RoundedPanel>().FirstOrDefault() is { } bubble && host.Tag?.ToString() == "message-user") bubble.Left = host.Width - bubble.Width - 4;
        }
    }

    private void ToggleTheme()
    {
        settings.ThemeMode = Theme.IsDark ? "light" : "dark"; Theme.SetMode(settings.ThemeMode); settings.Save(); themeButton.Text = Theme.IsDark ? "☀" : "☾"; ApplyThemeTree(this); NativeTheme.Apply(this); Invalidate(true);
    }

    private static void ApplyThemeTree(Control control)
    {
        var tag = control.Tag?.ToString();
        control.ForeColor = tag is "muted" or "response-meta" or "transparent-muted" ? Theme.Muted : Theme.Text;
        control.BackColor = tag switch
        {
            "sidebar" or "sidebar-selected" => Theme.Sidebar, "surface" or "composer" => Theme.Surface, "terminal" => Theme.Terminal, "terminal-input" => Theme.TerminalInput,
            "activity-running" => Theme.ActivityRunning, "activity-complete" => Theme.ActivityComplete, "activity-error" => Theme.ActivityError,
            "bubble-assistant" => Theme.AssistantBubble, "bubble-user" => Theme.UserBubble, "accent" => Theme.Accent,
            "transparent-muted" => Color.Transparent,
            "muted" or "response-meta" or "text" => control.Parent?.BackColor ?? Theme.Background,
            _ => control is Label ? control.Parent?.BackColor ?? Theme.Background : Theme.Background
        };
        if (control is ModernButton button)
        {
            button.NormalColor = tag == "accent" ? Theme.Accent : tag == "activity-running" ? Theme.ActivityRunning : tag == "activity-complete" ? Theme.ActivityComplete : tag == "activity-error" ? Theme.ActivityError : tag == "sidebar-selected" ? Theme.SurfaceHover : tag == "delete-chat" ? Theme.SurfaceHover : tag == "sidebar" ? Theme.Sidebar : Theme.Surface;
            button.HoverColor = tag == "accent" ? Theme.AccentHover : tag == "delete-chat" ? Color.FromArgb(115, 52, 57) : Theme.SurfaceHover; button.BorderColor = tag is "sidebar" or "sidebar-selected" or "delete-chat" ? Theme.Sidebar : Theme.Border;
            button.ForeColor = tag == "accent" ? Color.White : tag == "tool-error" ? Color.FromArgb(225, 92, 92) : tag == "muted" ? Theme.Muted : Theme.Text; button.Invalidate();
        }
        if (control is ModernDropdown dropdown) dropdown.Invalidate();
        if (control is MarkdownRichTextBox markdown) markdown.ApplyMarkdownTheme();
        if (control is RoundedPanel rounded) { rounded.BorderColor = Theme.Border; rounded.Invalidate(); }
        foreach (Control child in control.Controls) ApplyThemeTree(child);
    }

    private static ModernButton MakeButton(string text, int height, string tag)
        => new() { Text = text, Height = height, ForeColor = Theme.Text, Font = Theme.Ui, Tag = tag, Radius = 9, NormalColor = tag == "sidebar" ? Theme.Sidebar : Theme.Surface, HoverColor = Theme.SurfaceHover, BorderColor = tag == "sidebar" ? Theme.Sidebar : Theme.Border, DrawBorder = tag != "sidebar" };
}
