import path from "node:path";
import { createJiti } from "./node_modules/@earendil-works/pi-coding-agent/node_modules/jiti/lib/jiti.mjs";

const jiti = createJiti(import.meta.url);
const { approvalDecision } = await jiti.import(path.resolve("pi-gui-approval-extension.ts"));
const cwd = "C:\\workspace";
const read = { toolName: "read", input: { path: "README.md" } };
const bash = { toolName: "bash", input: { command: "npm test" } };
const write = { toolName: "write", input: { path: "src/app.cs", content: "test" } };
const unsafe = { toolName: "bash", input: { command: "git push origin main" } };
const outside = { toolName: "read", input: { path: "C:\\other\\secret.txt" } };

assertDecision(read, "ask", "allow");
assertDecision(bash, "ask", "confirm");
assertDecision(write, "ask", "confirm");
assertDecision(read, "auto", "allow");
assertDecision(bash, "auto", "allow");
assertDecision(unsafe, "auto", "confirm");
assertDecision(outside, "auto", "confirm");
assertDecision(read, "full", "allow");
assertDecision(unsafe, "full", "allow");
assertDecision(read, "custom", "confirm");
assertDecision(write, "custom", "confirm");

console.log("Permission policy smoke test passed.");

function assertDecision(event, mode, expected) {
  const actual = approvalDecision(event, cwd, mode);
  if (actual !== expected) throw new Error(`${mode}/${event.toolName}: expected ${expected}, received ${actual}`);
}
