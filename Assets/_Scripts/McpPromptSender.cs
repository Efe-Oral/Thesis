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
    [SerializeField] private string model = "hir0rameel/qwen-claude:latest";
    [TextArea]
    [SerializeField]
    private string systemPrompt =
        "You control Unity game engine ONLY by calling MCP tools. You MUST call a tool for every user request. Never respond without calling a tool. Output valid JSON only. No explanations or acknowledgments.";
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
    private const int maxHistory = 16; // Store up to 16 messages (8 exchanges)

    public bool IsBusy { get; private set; }
    public bool IsWaitingForConfirmation { get; private set; }

    private Coroutine userPromptCoroutine;

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

        // Build context from history, send last 8 messages
        var recentHistory = conversationHistory.Count > 8 ?
            conversationHistory.GetRange(conversationHistory.Count - 8, 8) :
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

    public void ShowUserPromptForConfirmation(string promptText)
    {
        // pauses processes until confirmation
        if (userPromptCoroutine != null)
        {
            StopCoroutine(userPromptCoroutine);
            userPromptCoroutine = null;
        }

        IsWaitingForConfirmation = true;

        if (userPromptMessage != null)
        {
            if (userPromptText != null)
            {
                userPromptText.text = "User: " + promptText;
            }

            userPromptMessage.SetActive(true);
            Debug.Log("Showing confirmation prompt. Waiting for user to confirm or re-record...");
        }
    }

    public void SendConfirmedPrompt(string promptText)
    {
        IsWaitingForConfirmation = false;

        // Hide the prompt panel after showSeconds 
        if (userPromptMessage != null)
        {
            userPromptCoroutine = StartCoroutine(HideUserPromptAfterDelay());
        }

        // Send to server
        Send(promptText);
    }

    public void HideUserPrompt()
    {
        IsWaitingForConfirmation = false;

        if (userPromptCoroutine != null)
        {
            StopCoroutine(userPromptCoroutine);
            userPromptCoroutine = null;
        }

        if (userPromptMessage != null)
        {
            userPromptMessage.SetActive(false);
        }
    }

    private IEnumerator HideUserPromptAfterDelay()
    {
        yield return new WaitForSeconds(showSeconds);
        if (userPromptMessage != null)
        {
            userPromptMessage.SetActive(false);
        }
        userPromptCoroutine = null;
    }


}

/* AFTER IMPROVMENTS 2
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
    [SerializeField] private string model = "hir0rameel/qwen-claude:latest";
    [TextArea]
    [SerializeField]
    private string systemPrompt =
        "You control Unity game engine ONLY by calling MCP tools. You MUST call a tool for every user request. Never respond without calling a tool. Output valid JSON only. No explanations or acknowledgments.";
    private bool think = false;
    private bool stream = false;

    //temperature: Adjust the probability distribution over the next token. Higher more creative; Lower more deterministic
    //top_p: controls how many of the most likely words the model can choose from. 
    // Higher top_p: model considers more tokens for the next; Lower top_p: model restricts to only very top tokens and becomes more focused, less diverse
    [SerializeField, Range(0f, 1f)] private float temperature = 0.2f;
    [SerializeField, Range(0f, 1f)] private float topP = 0.9f;

    [TextArea] public string lastPrompt;
    [TextArea(17, 10)] public string LLMResponse;

    [Header("Performance & Context Management")]
    [Tooltip("Maximum conversation messages to keep. Your current setup works well, only adjust if needed")]
    [SerializeField] private int maxHistoryMessages = 16; // Keep your current working size (8 exchanges)

    [Tooltip("Enable auto-reset only for very long sessions (20+ exchanges)")]
    [SerializeField] private bool enableAutoReset = false;
    [SerializeField] private int autoResetAfterExchanges = 20; // Only reset after 20+ exchanges when degradation starts

    [Tooltip("Compress responses only after this many exchanges to preserve early session quality")]
    [SerializeField] private bool enableLateSessionCompression = true;
    [SerializeField] private int startCompressionAfterExchanges = 15; // Only compress after 15 exchanges

    private List<Message> conversationHistory = new List<Message>(); // Changed to Message type for proper structure
    private int exchangeCount = 0;

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

    [ContextMenu("Clear Context (in Play mode)")]
    public void ClearContextFromInspector()
    {
        ClearConversationHistory();
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
        exchangeCount++;

        // Auto-reset if enabled and threshold reached
        if (enableAutoReset && exchangeCount >= autoResetAfterExchanges)
        {
            conversationHistory.Clear();
            exchangeCount = 0;
            Debug.Log($"Auto-reset: Context cleared after {autoResetAfterExchanges} exchanges");
        }

        // Add user message to history
        conversationHistory.Add(new Message("user", userPrompt));

        // Limit history size - keep recent exchanges for immediate references
        while (conversationHistory.Count > maxHistoryMessages)
        {
            conversationHistory.RemoveAt(0);
        }

        // Build proper message structure with roles
        var messages = BuildProperMessages();

        var payload = new ChatRequest
        {
            model = model,
            messages = messages,
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

    /// <summary>
    /// Builds properly structured messages with roles for the API
    /// </summary>
    private List<Message> BuildProperMessages()
    {
        var messages = new List<Message>();

        // Always start with system prompt
        messages.Add(new Message("system", systemPrompt));

        // Add conversation history in proper format
        messages.AddRange(conversationHistory);

        return messages;
    }

    /// <summary>
    /// Extracts key information from verbose JSON response
    /// </summary>
    private string CompressResponse(string fullJsonResponse)
    {
        // Keep response short but informative for context
        // This preserves tool calling accuracy while reducing tokens

        if (fullJsonResponse.Length < 100)
            return fullJsonResponse; // Already short

        // Check if it's a successful tool call
        if (fullJsonResponse.Contains("\"success\": true"))
        {
            // Extract tool name if present
            if (fullJsonResponse.Contains("tool_name"))
            {
                return "Tool executed successfully";
            }
            return "Action completed";
        }
        else if (fullJsonResponse.Contains("\"success\": false"))
        {
            // Keep error info for context
            if (fullJsonResponse.Contains("error"))
            {
                return "Action failed - check previous command";
            }
            return "Action failed";
        }

        // Fallback: keep first 80 chars for context
        return fullJsonResponse.Substring(0, Mathf.Min(80, fullJsonResponse.Length)) + "...";
    }

    private IEnumerator UpdateConversationHistory(string content)
    {
        // Only compress responses in late sessions to preserve early session quality
        bool shouldCompress = enableLateSessionCompression && exchangeCount > startCompressionAfterExchanges;
        string contentToStore = shouldCompress ? CompressResponse(content) : content;

        conversationHistory.Add(new Message("assistant", contentToStore));

        while (conversationHistory.Count > maxHistoryMessages)
        {
            conversationHistory.RemoveAt(0);
        }
        yield break;
    }

    // Clear conversation history if needed
    public void ClearConversationHistory()
    {
        conversationHistory.Clear();
        exchangeCount = 0;
        Debug.Log("Conversation history cleared");
    }
}
*/