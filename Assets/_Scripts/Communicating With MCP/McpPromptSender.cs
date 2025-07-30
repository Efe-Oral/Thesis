using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class McpPromptSender : MonoBehaviour
{
    private const string MCP_PROMPT_URL = "http://localhost:6400/prompt";

    [ContextMenu("Send Prompt To MCP")]
    public void SendPrompt()
    {
        string userPrompt = "Create a red cube at (0, 2, 0)";
        StartCoroutine(SendPromptToMcp(userPrompt));
    }

    private IEnumerator SendPromptToMcp(string prompt)
    {
        var requestBody = new JObject
        {
            ["prompt"] = prompt
        };

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody.ToString());
        using UnityWebRequest req = new UnityWebRequest(MCP_PROMPT_URL, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("LLM/MCP Response: " + req.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Prompt call failed: " + req.error);
        }
    }
}
