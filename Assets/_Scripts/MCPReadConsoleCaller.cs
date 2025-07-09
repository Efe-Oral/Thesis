using System;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq; // Requires Newtonsoft.Json package (see below)

public class MCPReadConsoleCaller : MonoBehaviour
{
    public string mcpHost = "127.0.0.1";
    public int mcpPort = 6500;

    [ContextMenu("Call MCP read_console")]
    public void CallReadConsole()
    {
        StartCoroutine(CallReadConsoleCoroutine());
    }

    private IEnumerator CallReadConsoleCoroutine()
    {
        bool done = false;
        string result = null;
        Exception error = null;

        // Run the blocking code in a background thread
        System.Threading.Thread thread = new System.Threading.Thread(() =>
        {
            try
            {
                using (TcpClient client = new TcpClient(mcpHost, mcpPort))
                using (NetworkStream stream = client.GetStream())
                {
                    string commandJson = @"{
                        ""type"": ""read_console"",
                        ""params"": {
                            ""action"": ""get"",
                            ""types"": [""log"", ""warning"", ""error""],
                            ""count"": 10
                        }
                    }";

                    byte[] data = Encoding.UTF8.GetBytes(commandJson);
                    stream.Write(data, 0, data.Length);

                    byte[] buffer = new byte[16 * 1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    result = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            done = true;
        });
        thread.Start();

        // Wait for the thread to finish
        while (!done)
            yield return null;

        if (error != null)
            Debug.LogError("Error calling MCP read_console: " + error.Message);
        else
        {
            try
            {
                var root = JObject.Parse(result);
                var data = root["result"]?["data"] as JArray;
                if (data != null)
                {
                    foreach (var entry in data)
                    {
                        string type = entry["type"]?.ToString();
                        string message = entry["message"]?.ToString();
                        Debug.Log($"[{type}] {message}");
                    }
                }
                else
                {
                    Debug.LogWarning("No log entries found in MCP response.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to parse MCP response: " + ex.Message);
            }
        }
    }
}