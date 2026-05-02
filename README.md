<img width="1395" height="170" alt="thesis-ascii-art" src="https://github.com/user-attachments/assets/fba50dcd-2c52-4728-a6e8-2c394b71de94" />

> **LLM-powered voice interaction in Virtual Reality**. A Unity-based master thesis project that connects a VR environment through a local or remote Ollama LLM via the Model Context Protocol (MCP), enabling natural-language voice commands to control the scene in near real-time. Complete master's thesis white paper can be found in this repo at [thesis whie paper.](thesis_white_paper.pdf)


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

### 1. Clone the repositories
Clone this repository and [ollama-mcp-bridge](https://github.com/jonigl/ollama-mcp-bridge) repository and set it up.
```bash
# Thesis project
git clone https://github.com/Efe-Oral/Thesis.git

# MCP bridge (required dependency)
git clone https://github.com/jonigl/ollama-mcp-bridge.git
```
<br>

### 2. Pull the language model
Pull the [language model](https://ollama.com/hir0rameel/qwen-claude:latest) from Ollama.
```bash
ollama pull hir0rameel/qwen-claude
```
<br>

### 3. Open the Unity project

Open the project in **Unity Hub** using version **2022.3.51f1**.
All packages and the MCP server install automatically on first launch.

Load the scene: `ThesisScene`

<br>

### 4. Apply override scripts

Copy the override scripts from `Assets/_Scripts/Override_Scripts/` to their targets.
See `README_OVERRIDE` in the same folder for details.

<br>


### 5. Configure the MCP bridge

Open and edit `mcp-config.json` file under: `ollama-mcp-bridge/mcp-servers-config/mcp-config.json` folder cloned from **ollama-mcp-bridge** repository. 

Update the JSON path to match your system:

![ezgif-85c67d2359e51008](https://github.com/user-attachments/assets/20e427f3-05e9-4258-ae9f-5851c74f09c9)

<br>







### 6. Start the MCP bridge

<details>
<summary><b>Option A — Ollama LLM deployed locally</b></summary>

Start Ollama in a terminal:

```bash
ollama serve
```

Then, open a new terminal inside `ollama-mcp-bridge` folder and start the bridge. Make sure that ollama is running before starting the bridge.


```bash
ollama-mcp-bridge --config mcp-servers-config/mcp-config.json
```
</details>

<details>
<summary><b>Option B (recommended) — Ollama LLM deployed on university server (requires VPN connection)</b></summary>

1. Connect to the [University VPN](https://www.rz.uni-wuerzburg.de/dienste/it-sicherheit/vpn/endgeraete/anyconnect/).
2. Open a new terminal inside `ollama-mcp-bridge` folder:

```bash
ollama-mcp-bridge --config mcp-servers-config/mcp-config.json --ollama-url http://10.85.8.40:11434
```

</details>

<br>


### 7. Enter Play Mode in Unity
You can now use the project.

<br>

---

<br>

### Usage

See the **controller button layout** image in the `Assets` folder to get familiar with the controls. 

In Play mode, you can press the `Space` key to start recording speech.

Alternatively, for manual testing without voice input:

1. In the **Hierarchy**, select the `MCP Prompt Sender` GameObject.
2. Type your prompt in the **Test Prompt** field in the Inspector
3. Right-click **Send Prompt (in Play mode)** to send it. Note that you can only send prompts during Play mode:


![ezgif-178c91db20cf32c21-ezgif com-video-to-gif-converter](https://github.com/user-attachments/assets/42b22d6e-f496-4270-9b48-4e0e2596c46a)

<br>
<br>

## Troubleshooting

---

If you find this project interesting and want to learn more about technical background, related work, methodology, and benchmarks, please refer to [thesis whie paper.](thesis_white_paper.pdf) <br />
Also, please feel free to reach out with questions, bug reports, or feedback.

**Efe Oral** - efeoral@gmail.com
