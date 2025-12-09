using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class McpPromptSender : MonoBehaviour
{

    [Header("Endpoint")]
    private string apiUrl = "http://localhost:8000/api/chat";
    private float requestTimeout = 30f; // Timeout in seconds

    [SerializeField] public GameObject userPromptMessage;
    [SerializeField] public TextMeshProUGUI userPromptText;
    [SerializeField] public GameObject successMessage;
    [SerializeField] private AudioClip successSound;
    [SerializeField] public GameObject errorMessage;
    [SerializeField] private AudioClip errorSound;
    [SerializeField] private float showSeconds = 2f;
    private AudioSource audioSource;
    private bool isSuccessMessageTrue = false;

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
    [TextArea(17, 10)] public string LLMResponse;

    // convarsation history for context
    private List<string> conversationHistory = new List<string>();
    private const int maxHistory = 6; // 6 back and forth interactions = 3 dialogs

    public bool IsBusy { get; private set; }

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

    // Public method to show user prompt from SpeechRecognition
    public void ShowUserPrompt(string promptText)
    {
        StartCoroutine(ShowUserPromptMessage(promptText));
    }

    private IEnumerator ShowUserPromptMessage(string promptText)
    {
        if (userPromptMessage != null)
        {
            if (userPromptText != null)
            {
                userPromptText.text = "User: " + promptText;
            }

            userPromptMessage.SetActive(true);
            yield return new WaitForSeconds(showSeconds);
            userPromptMessage.SetActive(false);
        }
    }

    private IEnumerator ShowLLMMessage()
    {

        if (!isSuccessMessageTrue)
        {
            errorMessage.SetActive(true);
            yield return new WaitForSeconds(showSeconds);
            errorMessage.SetActive(false);
        }

        if (successMessage != null && isSuccessMessageTrue)
        {
            successMessage.SetActive(true);
            yield return new WaitForSeconds(showSeconds);
            successMessage.SetActive(false);
            isSuccessMessageTrue = false;
        }
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private IEnumerator SendCoroutine(string userPrompt)
    {
        if (IsBusy)
            yield break;

        IsBusy = true;
        lastPrompt = userPrompt;

        // Add user message to history
        conversationHistory.Add("User: " + userPrompt);

        // Limit history size
        if (conversationHistory.Count > maxHistory)
        {
            conversationHistory.RemoveAt(0);
        }

        // Build context from history, but only include relevant parts
        var recentHistory = conversationHistory.Count > 4 ?
            conversationHistory.GetRange(conversationHistory.Count - 4, 4) :
            conversationHistory;
        string context = string.Join("\n", recentHistory);

        var payload = new ChatRequest
        {
            model = model,
            messages = new List<Message> {
                new Message("system", systemPrompt),
                new Message("user", context)
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
                LLMResponse = $"HTTP {req.responseCode} - {req.error}\n{req.downloadHandler.text}";
                Debug.LogError(LLMResponse);
                IsBusy = false;
                yield break;
            }

            var text = req.downloadHandler.text;
            ChatResponse resp = null;
            try { resp = JsonUtility.FromJson<ChatResponse>(text); } catch { }
            LLMResponse = (resp != null && resp.message != null) ? resp.message.content : text;

            // Handle UI feedback based on response
            bool isSuccess = LLMResponse.Contains("\"success\": true");
            isSuccessMessageTrue = isSuccess;

            // Start UI and audio feedback asycnhronously
            StartCoroutine(HandleFeedback(isSuccess));

            // Updateing conversation history asynchronously
            if (resp != null && resp.message != null)
            {
                StartCoroutine(UpdateConversationHistory(resp.message.content));
            }

            Debug.Log($"MCP response: {LLMResponse}");
        }

        IsBusy = false;
    }

    private IEnumerator HandleFeedback(bool isSuccess)
    {
        StartCoroutine(ShowLLMMessage());
        audioSource.PlayOneShot(isSuccess ? successSound : errorSound);
        yield break;
    }

    private IEnumerator UpdateConversationHistory(string content)
    {
        conversationHistory.Add("Assistant: " + content);
        if (conversationHistory.Count > maxHistory)
        {
            conversationHistory.RemoveAt(0);
        }
        yield break;
    }

    // Clear conversation history if needed
    public void ClearConversationHistory()
    {
        conversationHistory.Clear();
        Debug.Log("Conversation history cleared");
    }
}
