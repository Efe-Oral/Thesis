using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class McpPromptSender : MonoBehaviour
{

    [Header("Endpoint")]
    private string apiUrl = "http://localhost:8000/api/chat";

    [Header("Chat config")]
    [SerializeField] private string model = "qwen3:4b";
    [TextArea]
    [SerializeField]
    private string systemPrompt =
        "You control Unity game engine only by calling MCP tools exposed by unityMCP. Output valid JSON only. No explanations.";
    private bool think = false;
    private bool stream = false;

    //temperature: Adjust the probability distribution over the next token. Higher more creative; Lower more deterministic
    //top_p: controls how many of the most likely words the model can choose from. 
    // Higher top_p: model considers more tokens for the next; Lower top_p: model restricts to only very top tokens and becomes more focused, less diverse
    [SerializeField, Range(0f, 1f)] private float temperature = 0.2f;
    [SerializeField, Range(0f, 1f)] private float topP = 0.9f;

    [TextArea] public string lastPrompt;
    [TextArea(17, 10)] public string lastResponse;

    [System.Serializable]
    private class Message { public string role; public string content; public Message(string r, string c) { role = r; content = c; } }

    [System.Serializable]
    private class Options { public float temperature; public float top_p; }

    [System.Serializable]
    private class ChatRequest
    {
        public string model;
        public List<Message> messages;
        public bool think;
        public bool stream;
        public Options options;
    }

    [System.Serializable] private class ChatResponseMessage { public string role; public string content; }
    [System.Serializable] private class ChatResponse { public ChatResponseMessage message; public bool done; public string error; }

    public void Send(string userPrompt) { StartCoroutine(SendCoroutine(userPrompt)); }


    [Header("Testing")]
    [TextArea] public string testPrompt = "Create 3 cubes with meshes";

    [ContextMenu("Send Prompt (in Play mode)")]
    public void SendFromInspector()
    {
        StartCoroutine(SendCoroutine(testPrompt));
    }

    private IEnumerator SendCoroutine(string userPrompt)
    {
        lastPrompt = userPrompt;

        var payload = new ChatRequest
        {
            model = model,
            messages = new List<Message> {
                new Message("system", systemPrompt),
                new Message("user", userPrompt)
            },
            think = think,
            stream = stream,
            options = new Options { temperature = temperature, top_p = topP }
        };

        var json = JsonUtility.ToJson(payload);
        using (var req = new UnityWebRequest(apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                lastResponse = $"HTTP {req.responseCode} - {req.error}\n{req.downloadHandler.text}";
                Debug.LogError(lastResponse);
                yield break;
            }

            var text = req.downloadHandler.text;
            ChatResponse resp = null;
            try { resp = JsonUtility.FromJson<ChatResponse>(text); } catch { }
            lastResponse = (resp != null && resp.message != null) ? resp.message.content : text;
            Debug.Log($"MCP response: {lastResponse}");
        }
    }
}
