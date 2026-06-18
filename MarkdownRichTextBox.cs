using System.Text;
using System.Text.RegularExpressions;

namespace PiGUI;

internal sealed class MarkdownRichTextBox : RichTextBox
{
    private static readonly Regex InlineMarkdown = new(
        @"(\*\*\*.+?\*\*\*|___.+?___|\*\*.+?\*\*|__.+?__|~~.+?~~|`[^`]+`|\[[^\]]+\]\([^)]+\)|(?<!\*)\*[^*\r\n]+\*(?!\*)|(?<!_)_[^_\r\n]+_(?!_))",
        RegexOptions.Compiled);

    private readonly StringBuilder markdown = new();
    private Color? colorOverride;

    public int MarkdownLength => markdown.Length;

    public MarkdownRichTextBox()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        DetectUrls = true;
        ScrollBars = RichTextBoxScrollBars.None;
        Font = Theme.Ui;
        TabStop = false;
    }

    public void SetMarkdown(string value, Color? textColor = null)
    {
        markdown.Clear();
        markdown.Append(value);
        colorOverride = textColor;
        RenderMarkdown();
    }

    public void AppendMarkdown(string value)
    {
        markdown.Append(value);
        RenderMarkdown();
    }

    public void ApplyMarkdownTheme() => RenderMarkdown();

    private void RenderMarkdown()
    {
        var scrollPosition = GetPositionFromCharIndex(TextLength);
        SuspendLayout();
        Clear();
        var baseColor = colorOverride ?? Theme.Text;
        var inCodeBlock = false;
        var lines = markdown.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                AppendStyled(line.Length == 0 ? " " : line, Theme.Mono, baseColor, Theme.SurfaceAlt);
            }
            else
            {
                RenderLine(line, baseColor);
            }

            if (index < lines.Length - 1) AppendText("\n");
        }

        SelectionStart = TextLength;
        SelectionLength = 0;
        ResumeLayout();
        if (scrollPosition.Y > ClientSize.Height) ScrollToCaret();
    }

    private void RenderLine(string line, Color baseColor)
    {
        var trimmed = line.TrimStart();
        var indent = line[..(line.Length - trimmed.Length)];
        var headingLevel = 0;
        while (headingLevel < trimmed.Length && headingLevel < 6 && trimmed[headingLevel] == '#') headingLevel++;
        if (headingLevel > 0 && headingLevel < trimmed.Length && trimmed[headingLevel] == ' ')
        {
            var size = headingLevel switch { 1 => 17F, 2 => 15F, 3 => 13F, _ => 11F };
            using var headingFont = new Font("Segoe UI Semibold", size, FontStyle.Bold);
            AppendInline(trimmed[(headingLevel + 1)..], headingFont, baseColor);
            return;
        }

        if (Regex.IsMatch(trimmed, @"^([-*_])\1{2,}\s*$"))
        {
            AppendStyled("────────────────────────", Theme.Small, Theme.Muted);
            return;
        }

        if (trimmed.StartsWith("> ", StringComparison.Ordinal))
        {
            using var quoteFont = new Font(Theme.Ui, FontStyle.Italic);
            AppendStyled("▎ ", quoteFont, Theme.Accent);
            AppendInline(trimmed[2..], quoteFont, Theme.Muted);
            return;
        }

        var bullet = Regex.Match(trimmed, @"^[-*+]\s+(.*)$");
        if (bullet.Success)
        {
            AppendStyled(indent + "•  ", Theme.Ui, Theme.Accent);
            AppendInline(bullet.Groups[1].Value, Theme.Ui, baseColor);
            return;
        }

        AppendInline(line, Theme.Ui, baseColor);
    }

    private void AppendInline(string text, Font baseFont, Color baseColor)
    {
        var position = 0;
        foreach (Match match in InlineMarkdown.Matches(text))
        {
            if (match.Index > position) AppendStyled(text[position..match.Index], baseFont, baseColor);
            var token = match.Value;
            if ((token.StartsWith("***") && token.EndsWith("***")) || (token.StartsWith("___") && token.EndsWith("___")))
                AppendWithStyle(token[3..^3], baseFont, baseColor, FontStyle.Bold | FontStyle.Italic);
            else if ((token.StartsWith("**") && token.EndsWith("**")) || (token.StartsWith("__") && token.EndsWith("__")))
                AppendWithStyle(token[2..^2], baseFont, baseColor, FontStyle.Bold);
            else if (token.StartsWith("~~") && token.EndsWith("~~"))
                AppendWithStyle(token[2..^2], baseFont, Theme.Muted, FontStyle.Strikeout);
            else if (token.StartsWith('`') && token.EndsWith('`'))
                AppendStyled(token[1..^1], Theme.Mono, baseColor, Theme.SurfaceAlt);
            else if (token.StartsWith('['))
            {
                var split = token.IndexOf("](", StringComparison.Ordinal);
                var label = token[1..split];
                var url = token[(split + 2)..^1];
                AppendWithStyle(label, baseFont, Theme.Accent, FontStyle.Underline);
                AppendStyled($" ({url})", Theme.Small, Theme.Muted);
            }
            else
                AppendWithStyle(token[1..^1], baseFont, baseColor, FontStyle.Italic);
            position = match.Index + match.Length;
        }
        if (position < text.Length) AppendStyled(text[position..], baseFont, baseColor);
    }

    private void AppendWithStyle(string text, Font baseFont, Color color, FontStyle style)
    {
        using var font = new Font(baseFont, baseFont.Style | style);
        AppendStyled(text, font, color);
    }

    private void AppendStyled(string text, Font font, Color color, Color? background = null)
    {
        SelectionStart = TextLength;
        SelectionLength = 0;
        SelectionFont = font;
        SelectionColor = color;
        SelectionBackColor = background ?? BackColor;
        SelectedText = text;
    }
}
