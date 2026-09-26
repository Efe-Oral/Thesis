"""
Starts ollama-mcp-bridge with MCP tools exposed under their real names.

Why: the bridge renames every tool to "<server>.<tool>" (e.g. "unityMCP.light_caster").
With many tools, the local model writes the built-in MCP for Unity tool names without
that prefix (e.g. "manage_gameobject"), and Ollama silently drops the call because the
name doesn't match. Keeping the original names fixes this, so all tools stay usable.

The patch is applied in memory only; the installed ollama-mcp-bridge package is untouched.
It assumes a single MCP server in the config (tool names must be unique).

Usage (from the ollama-mcp-bridge folder, same arguments as ollama-mcp-bridge):
    python "D:\\Unity Projects\\Thesis\\Tools\\start_bridge.py" --config mcp-servers-config/mcp-config.json --ollama-url http://10.85.8.40:11434
"""
from loguru import logger
from ollama_mcp_bridge import mcp_manager
from ollama_mcp_bridge.main import main

_original_connect = mcp_manager.MCPManager._connect_server


async def _connect_server_unprefixed(self, name, config):
    await _original_connect(self, name, config)
    renamed = 0
    for tool in self.all_tools:
        if tool.get("server") == name and tool["function"]["name"] != tool["original_name"]:
            tool["function"]["name"] = tool["original_name"]
            renamed += 1
    names = [t["function"]["name"] for t in self.all_tools]
    duplicates = {n for n in names if names.count(n) > 1}
    if duplicates:
        logger.warning(f"Tool names are not unique across servers: {sorted(duplicates)}")
    logger.info(f"[start_bridge] Exposing {renamed} '{name}' tools under their original names (no '{name}.' prefix)")


mcp_manager.MCPManager._connect_server = _connect_server_unprefixed

if __name__ == "__main__":
    main()
