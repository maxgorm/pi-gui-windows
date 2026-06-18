# Pi GUI for Windows

A lightweight native Windows desktop interface for the [pi agentic coding harness](https://pi.dev), inspired by [Pi GUI](https://pi-gui.com). It uses a WinForms/.NET shell rather than Electron and talks to pi through its documented JSONL RPC protocol.

## Features

- Uses ChatGPT Plus/Pro Codex or GitHub Copilot subscriptions through pi's OAuth providers; no API key required
- Codex models: GPT-5.5, GPT-5.4, and GPT-5.4 mini
- GitHub Copilot models: GPT-5.3 Codex, GPT-5.2 Codex, and Claude Opus 4.8
- Low, medium, high, and xhigh reasoning controls; defaults to GPT-5.5 on medium
- Native project-folder picker and remembered recent projects
- Light and dark themes with modern rounded controls
- Approval policies for mutating or potentially unsafe tool actions
- Streaming responses, tool activity, stop control, persistent pi sessions, and new chats
- Paste screenshots directly from the clipboard
- Attach or drag-and-drop images and local files
- Native Windows executable with the pi runtime kept as a pinned local dependency

## Requirements

- Windows 10 or 11
- [Node.js 22.19](https://nodejs.org/) or newer
- [.NET 7 SDK/runtime](https://dotnet.microsoft.com/download) or newer

## Quick start

Open PowerShell in this folder:

```powershell
.\setup.ps1
.\run.ps1
```

On first launch, click **Accounts + sign-in**, then connect Codex, GitHub Copilot, or both. Pi GUI opens the provider's secure webpage in your default browser. Codex completes through a browser callback; Copilot copies the device code and opens GitHub's activation page for you. No terminal login is used.

Pi stores and refreshes OAuth credentials in its standard `%USERPROFILE%\.pi\agent\auth.json` store.

## Usage

- Pick a project from the top-left folder control.
- Select Codex or GitHub Copilot, then choose one of that provider's models and a reasoning level.
- Choose an approval policy beside the model controls: **Ask for approval**, **Approve for me**, **Full access**, or **Custom**.
- Press Enter to send or Shift+Enter for a new line.
- Paste an image with Ctrl+V, click **Attach**, or drop files onto the window.
- Attached images are sent as multimodal input. Other files are supplied to the local agent as exact file paths so it can inspect them with its tools.
- Click **Stop** to abort the current agent operation and **New chat** to begin a fresh persisted pi session.

## Build and verification

```powershell
.\smoke-test.ps1
.\publish.ps1
```

`publish.ps1` creates a portable x64 build in `artifacts\win-x64`. The app remains framework-dependent to keep the download small.

## Architecture

The native C# UI launches the pinned `@earendil-works/pi-coding-agent` CLI in `--mode rpc`. Commands and streamed agent events travel over strict JSONL on stdin/stdout. A tiny local Node helper calls pi's own OAuth providers and hands browser URLs/device codes to the native Accounts window. Authentication, model calls, tools, context management, and session persistence remain owned by pi.

## Security

The native UI never receives your OAuth tokens. Authentication is performed by pi's OAuth providers, and the helper writes credentials only through pi's normal user-level auth store. The agent can read, edit, and run commands in the selected project; review the selected folder before sending work.

## License

MIT. This project is an independent Windows client and is not affiliated with OpenAI or the upstream Pi GUI authors.
