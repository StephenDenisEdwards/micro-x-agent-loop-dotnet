# WhatsApp MCP Server

## Overview

The WhatsApp MCP server gives the agent the ability to search, read, and send WhatsApp messages. It is an **external MCP server** — not bundled in this repository — based on the [lharries/whatsapp-mcp](https://github.com/lharries/whatsapp-mcp) project.

Unlike the system-info MCP server (which is a single stdio process), WhatsApp requires a **two-component architecture**: a Go bridge that maintains a persistent connection to WhatsApp Web, and a Python MCP server that the agent communicates with via stdio.

## Architecture

```
┌──────────────┐    stdio/JSONRPC    ┌──────────────────┐   HTTP :8080    ┌─────────────────┐   WebSocket   ┌──────────────┐
│  Agent Loop  │ ◄────────────────► │  Python MCP      │ ─────────────► │   Go Bridge     │ ◄──────────► │  WhatsApp    │
│  (this repo) │                    │  Server           │               │   (whatsmeow)   │              │  Web         │
└──────────────┘                    │                   │               │                 │              └──────────────┘
                                    │  reads SQLite  ──►│               │  writes SQLite  │
                                    └──────────────────┘               └─────────────────┘
                                              │                                  │
                                              └───────── messages.db ◄───────────┘
```

### Components

| Component | Language | Role |
|-----------|----------|------|
| **Go bridge** (`whatsapp-bridge/`) | Go | Connects to WhatsApp Web via the [whatsmeow](https://github.com/tulir/whatsmeow) library. Stores messages in a SQLite database. Exposes an HTTP API on port 8080 for sending messages and fetching contacts. |
| **Python MCP server** (`whatsapp-mcp-server/`) | Python | FastMCP server that reads the SQLite database for message history and calls the bridge HTTP API for sending. Communicates with the agent via stdio JSONRPC. |

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| [Go](https://go.dev/dl/) | 1.21+ | Build the WhatsApp bridge |
| [uv](https://docs.astral.sh/uv/) | any | Run the Python MCP server |
| GCC / C compiler | any | Required by go-sqlite3 (CGO dependency) |

### Windows-Specific: The CGO Problem

The Go bridge uses [go-sqlite3](https://github.com/mattn/go-sqlite3), which is a CGO package — it contains C code that must be compiled with a C compiler. This is the single biggest pain point on Windows, because Go's CGO toolchain requires GCC in `PATH`, and Windows does not ship with GCC.

**Options for getting GCC on Windows:**

| Option | Pros | Cons |
|--------|------|------|
| [WinLibs MinGW-w64](https://winlibs.com/) | Standalone, no installer bloat | Long path under `AppData\Local\Microsoft\WinGet\Packages\` |
| [MSYS2](https://www.msys2.org/) | Full Unix-like environment | Heavy |
| [TDM-GCC](https://jmeubank.github.io/tdm-gcc/) | Simple installer, short path | Less actively maintained |
| [Chocolatey mingw](https://community.chocolatey.org/packages/mingw) | `choco install mingw` | Requires Chocolatey |

## Setup

1. Clone the WhatsApp MCP repository
2. Build the Go bridge (with CGO_ENABLED=1)
3. Run the bridge and scan the QR code to authenticate
4. Wait for history sync (30-60 seconds)
5. Configure the MCP server in `appsettings.json`
6. Start the agent

See the [Python repo's WhatsApp MCP README](https://github.com/StephenDenisEdwards/micro-x-agent-loop-python/blob/main/documentation/docs/design/tools/whatsapp-mcp/README.md) for detailed setup instructions including Windows CGO troubleshooting.

## Tools

The WhatsApp MCP server exposes 12 tools. All tool names are prefixed with `whatsapp__` in the agent.

### Message Tools

| MCP Tool | Agent Tool Name | Description |
|----------|----------------|-------------|
| `list_messages` | `whatsapp__list_messages` | Search and filter messages |
| `get_message_context` | `whatsapp__get_message_context` | Get surrounding messages for context |
| `send_message` | `whatsapp__send_message` | Send a text message |
| `send_file` | `whatsapp__send_file` | Send a file |
| `send_audio_message` | `whatsapp__send_audio_message` | Send voice message |
| `download_media` | `whatsapp__download_media` | Download media from a message |

### Chat Tools

| MCP Tool | Agent Tool Name | Description |
|----------|----------------|-------------|
| `list_chats` | `whatsapp__list_chats` | List chats with search and pagination |
| `get_chat` | `whatsapp__get_chat` | Get chat metadata by JID |
| `get_direct_chat_by_contact` | `whatsapp__get_direct_chat_by_contact` | Find direct chat by phone number |
| `get_contact_chats` | `whatsapp__get_contact_chats` | Get all chats involving a contact |

### Contact Tools

| MCP Tool | Agent Tool Name | Description |
|----------|----------------|-------------|
| `search_contacts` | `whatsapp__search_contacts` | Search contacts by name or phone |
| `get_last_interaction` | `whatsapp__get_last_interaction` | Get most recent message with a contact |

## Known Limitations

1. **Bridge must run separately** — start it manually before using WhatsApp tools
2. **SQLite path is relative** — the repo must keep its default directory layout
3. **Bridge port is hardcoded** — port 8080
4. **Audio messages require ffmpeg**
5. **History sync on first connect only**
6. **Single WhatsApp account** per bridge instance
7. **Session expiry** — ~14 days of inactivity
8. **Client version expiry** — requires bridge rebuild when outdated

## Troubleshooting

See [Troubleshooting](../../../operations/troubleshooting.md) for general MCP issues and WhatsApp-specific problems.
