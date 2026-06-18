namespace PiGUI;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--runtime-stress", StringComparer.OrdinalIgnoreCase))
            return RuntimeSelfTest.RunAsync().GetAwaiter().GetResult();
        if (args.Contains("--ui-stress", StringComparer.OrdinalIgnoreCase))
            return UiInteractionSelfTest.Run();
        if (args.Contains("--approval-preview", StringComparer.OrdinalIgnoreCase))
        {
            Theme.SetMode("dark");
            Application.Run(new ApprovalForm("Approve this action?", "bash · npm install && npm test"));
            return 0;
        }
        Application.Run(new MainForm());
        return 0;
    }
}
