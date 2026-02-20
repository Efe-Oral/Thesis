<img width="1395" height="170" alt="thesis-ascii-art" src="https://github.com/user-attachments/assets/fba50dcd-2c52-4728-a6e8-2c394b71de94" />



## Requirements

| # | Tool | Version |
|:-:|:-----|:--------|
| 1 | Unity | 2022.3.51f1 |
| 2 | Python | ≥ 3.12 |
| 3 | `uv` | latest |
| 4 | Git | latest |
| 5 | Ollama | latest |

### Optional

| Tool | Purpose |
|:-----|:--------|
| VR Headset (e.g., Meta Quest 2) | VR interaction |
| University VPN | Access to remote Ollama server |

<br>

---
<br>

## Setup Instructions

**Step 1.** Clone Thesis repository

<br>

**Step 2.** Clone the [ollama-mcp-bridge](https://github.com/jonigl/ollama-mcp-bridge) repository and set it up

<br>

**Step 3.** Pull the [language model](https://ollama.com/hir0rameel/qwen-claude:latest) from Ollama:
```bash
ollama pull hir0rameel/qwen-claude
```
<br>

**Step 4.** Open the project in Unity Hub with version **2022.3.51f1** and load `ThesisScene`. All packages and the MCP server install automatically.

<br>

**Step 5.** Apply the override scripts from `Assets/_Scripts/Override_Scripts/`. See `README_OVERRIDE` in the same folder for more details.

<br>

**Step 6.** Open and edit `mcp-config.json` file under `ollama-mcp-bridge/mcp-servers-config/mcp-config.json` folder cloned from **ollama-mcp-bridge** repository. Paste the JSON path to match your system:


![ezgif-85c67d2359e51008](https://github.com/user-attachments/assets/20e427f3-05e9-4258-ae9f-5851c74f09c9)

<br>

**Step 7.** Connect to the university [VPN](https://www.rz.uni-wuerzburg.de/dienste/it-sicherheit/vpn/endgeraete/anyconnect/)
<br>

**Step 8.** Start the MCP bridge:

**Method 1 — Ollama LLM deployed locally:**

First, start Ollama:

```bash
ollama serve
```

Then, open a new terminal inside `ollama-mcp-bridge` folder:

```bash
ollama-mcp-bridge --config mcp-servers-config/mcp-config.json
```
<br>

**Method 2 (recommended) — Ollama LLM deployed on University Server (requires VPN connection):**

Connect to the university VPN, open a new terminal inside `ollama-mcp-bridge` folder:

```bash
ollama-mcp-bridge --config mcp-servers-config/mcp-config.json --ollama-url http://10.85.8.40:11434
```
<br>

**Step 9.** Enter Playmode in Unity.

See the `controller button layout` image in the `Assets` folder to get familiar with the controls. 

In Playmode, you can press the `Space` key to start recording speech.

Alternatively, find the `MCP Prompt Sender` gameobject in the Hierarchy. You can type your prompts in the **Test Prompt** field and right-click **Send Prompt (in Play mode)** to manually send them to the model:


![ezgif-178c91db20cf32c21-ezgif com-video-to-gif-converter](https://github.com/user-attachments/assets/42b22d6e-f496-4270-9b48-4e0e2596c46a)

<br>
<br>

## Troubleshooting

Please contact me if you have any questions
efeoral@gmail.com
