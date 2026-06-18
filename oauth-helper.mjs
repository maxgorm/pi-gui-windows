import { AuthStorage } from "@earendil-works/pi-coding-agent";

const providerId = process.argv[2];
if (!providerId || !["openai-codex", "github-copilot"].includes(providerId)) {
  process.stdout.write(JSON.stringify({ event: "error", message: "Unsupported provider" }) + "\n");
  process.exit(2);
}

const emit = (value) => process.stdout.write(JSON.stringify(value) + "\n");

try {
  const auth = AuthStorage.create();
  await auth.login(providerId, {
    onAuth: (info) => emit({ event: "open_url", url: info.url, instructions: info.instructions }),
    onDeviceCode: (info) => emit({ event: "device_code", ...info }),
    onPrompt: async (prompt) => {
      if (prompt.allowEmpty) return "";
      throw new Error("The browser callback did not complete. Please retry the sign-in flow.");
    },
    onProgress: (message) => emit({ event: "progress", message }),
    onSelect: async (prompt) => {
      const browser = prompt.options.find((option) => option.id.includes("browser"));
      return browser?.id ?? prompt.options[0]?.id;
    },
  });
  emit({ event: "complete", provider: providerId });
} catch (error) {
  emit({ event: "error", message: error instanceof Error ? error.message : String(error) });
  process.exitCode = 1;
}
