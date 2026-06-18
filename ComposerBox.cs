namespace PiGUI;

internal sealed class ComposerBox : RichTextBox
{
    private const int WmPaste = 0x0302;
    public event Action<Image>? ImagePasted;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPaste && Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image is not null) ImagePasted?.Invoke(new Bitmap(image));
            return;
        }
        base.WndProc(ref m);
    }
}
