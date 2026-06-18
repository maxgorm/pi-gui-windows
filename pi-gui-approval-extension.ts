const riskyCommand = /(^|\s)(rm\s+(-[^\s]*r|--recursive)|del\s+\/|rmdir|format|diskpart|shutdown|reboot|sudo|runas|curl|wget|invoke-webrequest|iwr\b|git\s+push|npm\s+publish|gh\s+pr\s+merge)(\s|$)/i;
const destructiveGit = /git\s+(reset\s+--hard|clean\s+-[a-z]*f|checkout\s+--|restore\s+[^\n]*--source)/i;

function describe(event: any): string {
  const input = event.input ?? {};
  if (event.toolName === "bash") return String(input.command ?? "Run a shell command");
  const target = input.path ?? input.file_path ?? input.filePath ?? "project files";
  return `${event.toolName} · ${target}`;
}

function outsideWorkspace(event: any, cwd: string): boolean {
  const input = event.input ?? {};
  const target = input.path ?? input.file_path ?? input.filePath;
  if (!target || !/^[a-zA-Z]:[\\/]/.test(String(target))) return false;
  const normalized = String(target).replaceAll("/", "\\").toLowerCase();
  return !normalized.startsWith(cwd.replaceAll("/", "\\").toLowerCase() + "\\");
}

export function approvalDecision(event: any, cwd: string, mode: string): "allow" | "confirm" {
  if (mode === "full") return "allow";

  const tool = String(event.toolName ?? "").toLowerCase();
  const command = String(event.input?.command ?? "");
  const mutating = ["bash", "write", "edit"].includes(tool);
  const unsafe = riskyCommand.test(command) || destructiveGit.test(command) || outsideWorkspace(event, cwd);

  if (mode === "auto") return unsafe ? "confirm" : "allow";
  if (mode === "custom") return "confirm";
  return mutating || unsafe ? "confirm" : "allow";
}

export default function approvalExtension(pi: any) {
  if (process.env.PI_GUI_APPROVAL_TEST === "1") {
    pi.registerCommand("approval-smoke", {
      description: "Exercise the approval UI protocol",
      handler: async (_args: string, ctx: any) => {
        await ctx.ui.confirm("Approval smoke test", "No command will be executed.");
      },
    });
  }
  pi.on("tool_call", async (event: any, ctx: any) => {
    const mode = process.env.PI_GUI_APPROVAL_MODE ?? "ask";
    const command = String(event.input?.command ?? "");
    const unsafe = riskyCommand.test(command) || destructiveGit.test(command) || outsideWorkspace(event, ctx.cwd ?? process.cwd());
    if (approvalDecision(event, ctx.cwd ?? process.cwd(), mode) === "allow") return;

    let approved = false;
    try {
      approved = await ctx.ui.confirm(
        unsafe ? "Potentially unsafe action" : "Approve this action?",
        describe(event),
      );
    } catch (error) {
      return { block: true, reason: `Approval UI failed: ${error instanceof Error ? error.message : String(error)}` };
    }
    if (!approved) return { block: true, reason: "Action denied by the user" };
  });
}
