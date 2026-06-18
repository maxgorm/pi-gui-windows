namespace PiGUI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--approval-preview", StringComparer.OrdinalIgnoreCase))
        {
            Theme.SetMode("dark");
            Application.Run(new ApprovalForm("Approve this action?", "bash · npm install && npm test"));
            return;
        }
        Application.Run(new MainForm());
    }
}
