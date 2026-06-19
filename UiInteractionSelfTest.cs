using System.Reflection;
using System.Text.Json;

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
        var markdown = new MarkdownRichTextBox { Location = new Point(20, 70), Width = 260 };
        markdown.SetMarkdown("# Status\n   I’m **ready** with `code`.\n    - First item");
        form.Controls.Add(markdown);
        var streamingMarkdown = new MarkdownRichTextBox();
        streamingMarkdown.SetMarkdown("");
        streamingMarkdown.AppendStreamingMarkdown("**smooth");
        streamingMarkdown.AppendStreamingMarkdown(" output**");
        if (streamingMarkdown.Text != "**smooth output**")
            throw new InvalidOperationException("Streaming text was not appended incrementally.");
        streamingMarkdown.FinalizeMarkdown();
        if (streamingMarkdown.Text != "smooth output")
            throw new InvalidOperationException("Streaming Markdown did not finalize correctly.");
        var activity = new ActivityTimelinePanel { Width = 300 };
        activity.ShowThinking();
        activity.StartTool("read-1", "read", "README.md");
        activity.FinishTool("read-1", "read", false, "");
        activity.Complete();
        var activityHeader = activity.Controls.OfType<ModernButton>().Single();
        activityHeader.PerformClick();
        if (activity.Height <= 30 || activity.Height > 230)
            throw new InvalidOperationException("Activity timeline did not expand within its height cap.");
        activityHeader.PerformClick();
        if (activity.Height != 30) throw new InvalidOperationException("Activity timeline did not collapse.");
        var sampleChanges = new TurnChanges
        {
            ProjectPath = Path.GetTempPath(),
            Patch = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-old\n+new\n",
            Files = new[] { new ChangedFile("a.txt", 1, 1) }
        };
        using var changeSummary = new ChangeSummaryPanel(sampleChanges);
        using var diffReview = new DiffReviewPanel(); diffReview.SetChanges(sampleChanges);
        if (changeSummary.Height < 80 || diffReview.Controls.Count == 0) throw new InvalidOperationException("Change review controls did not initialize.");
        var usage = new ResponseUsageTracker(id => id);
        usage.Begin("github-copilot", "gpt-5.4");
        using (var message = JsonDocument.Parse("""{"role":"assistant","provider":"github-copilot","model":"gpt-5.4","responseModel":"gpt-5.5","usage":{"totalTokens":123,"cost":{"total":0.02}}}"""))
            usage.AddMessage(message.RootElement);
        var usageText = usage.Finish();

        ThreadExceptionEventHandler threadException = (_, e) => { failure = e.Exception; form.Close(); };
        Application.ThreadException += threadException;
        form.Shown += (_, _) => form.BeginInvoke(() =>
        {
            try
            {
                typeof(ModernDropdown).GetMethod("ShowMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dropdown, null);
                var menu = (ContextMenuStrip?)typeof(ModernDropdown).GetField("activeMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dropdown)
                    ?? throw new InvalidOperationException("The drop-down menu did not open.");
                if (!markdown.Text.Contains("I’m ready with code.", StringComparison.Ordinal) ||
                    markdown.Text.Contains("**", StringComparison.Ordinal) ||
                    !markdown.Text.Contains("•  First item", StringComparison.Ordinal) ||
                    markdown.Text.Contains("    •", StringComparison.Ordinal))
                    throw new InvalidOperationException("Markdown or Unicode chat rendering failed.");
                if (!usageText.Contains("Actual: gpt-5.5", StringComparison.Ordinal) ||
                    !usageText.Contains("requested gpt-5.4", StringComparison.Ordinal) ||
                    !usageText.Contains("123 tokens", StringComparison.Ordinal) ||
                    !usageText.Contains("~7.5 premium credits", StringComparison.Ordinal))
                    throw new InvalidOperationException("Response model or usage attribution failed.");
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
