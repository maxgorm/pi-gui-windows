import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

const toolsRoot = path.resolve("node_modules/@earendil-works/pi-coding-agent/dist/core/tools");
const load = (name) => import(pathToFileURL(path.join(toolsRoot, `${name}.js`)).href);
const [{ createBashTool }, { createReadTool }, { createWriteTool }, { createEditTool }] = await Promise.all([
  load("bash"), load("read"), load("write"), load("edit"),
]);

const temp = await fs.mkdtemp(path.join(os.tmpdir(), "pi-gui-tools-"));
try {
  const bash = createBashTool(temp);
  const bashResult = await bash.execute("bash-smoke", { command: "printf pi-gui-tool-smoke" });
  assertText(bashResult, "pi-gui-tool-smoke", "bash");

  const write = createWriteTool(temp);
  await write.execute("write-smoke", { path: "sample.txt", content: "alpha" });

  const read = createReadTool(temp);
  const readResult = await read.execute("read-smoke", { path: "sample.txt" });
  assertText(readResult, "alpha", "read");

  const edit = createEditTool(temp);
  const editArgs = edit.prepareArguments?.({ path: "sample.txt", edits: [{ oldText: "alpha", newText: "beta" }] })
    ?? { path: "sample.txt", edits: [{ oldText: "alpha", newText: "beta" }] };
  await edit.execute("edit-smoke", editArgs);
  if ((await fs.readFile(path.join(temp, "sample.txt"), "utf8")) !== "beta") throw new Error("edit tool did not update the file");
  console.log("Bash, read, write, and edit tool smoke tests passed.");
} finally {
  await fs.rm(temp, { recursive: true, force: true });
}

function assertText(result, expected, tool) {
  const text = result.content?.filter((item) => item.type === "text").map((item) => item.text).join("\n") ?? "";
  if (!text.includes(expected)) throw new Error(`${tool} tool output did not contain ${expected}: ${text}`);
}
