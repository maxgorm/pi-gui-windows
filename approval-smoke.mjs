import { spawn } from "node:child_process";
import path from "node:path";

const cli = path.resolve("node_modules/@earendil-works/pi-coding-agent/dist/cli.js");
const extension = path.resolve("pi-gui-approval-extension.ts");
const child = spawn(process.execPath, [cli, "--mode", "rpc", "--provider", "openai-codex", "--model", "gpt-5.5", "--approve", "--extension", extension], {
  cwd: process.cwd(), env: { ...process.env, PI_GUI_APPROVAL_MODE: "ask", PI_GUI_APPROVAL_TEST: "1" }, stdio: ["pipe", "pipe", "pipe"],
});

let buffer = "";
let passed = false;
const timer = setTimeout(() => finish(new Error("Approval RPC request timed out")), 15000);

child.stdout.on("data", (chunk) => {
  buffer += chunk.toString("utf8");
  for (;;) {
    const newline = buffer.indexOf("\n");
    if (newline < 0) break;
    const line = buffer.slice(0, newline).replace(/\r$/, ""); buffer = buffer.slice(newline + 1);
    if (!line) continue;
    const event = JSON.parse(line);
    if (event.type === "extension_ui_request" && event.method === "confirm") {
      passed = true;
      child.stdin.write(JSON.stringify({ type: "extension_ui_response", id: event.id, confirmed: false }) + "\n");
      finish();
    }
  }
});
child.on("error", finish);
child.stdin.write(JSON.stringify({ id: "approval-smoke", type: "prompt", message: "/approval-smoke" }) + "\n");

function finish(error) {
  clearTimeout(timer);
  try { child.kill(); } catch {}
  if (error || !passed) { console.error(error?.message ?? "Approval RPC test failed"); process.exitCode = 1; }
  else console.log("Approval RPC smoke test passed.");
}
