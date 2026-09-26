using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SpeechRecognition : MonoBehaviour
{
    [Header("MCP Prompt Sender")]
    [SerializeField] private McpPromptSender mcpPromptSender;

    [Header("Confirmation Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button recordAgainButton;
    [SerializeField] private AudioClip confirmSound;
    [SerializeField] private AudioClip cancelSound;

    [Header("Look Detection")]
    [SerializeField] private AutomaticDescriptor automaticDescriptor;

    [Header("Input Busy UI")]
    [SerializeField] private GameObject busyPanel;
    [SerializeField] private float busyShowSeconds = 1.0f;

    [Header("Azure Speech Settings")]
    [Tooltip("Leave empty. The key is read from the AZURE_SPEECH_KEY environment variable or SpeechKey.local.txt in the project root (git-ignored).")]
    [SerializeField] private string speechKey = "";
    [SerializeField] private string speechRegion = "germanywestcentral"; // e.g. "germanywestcentral"

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip endSound;
    [SerializeField] private AudioClip buzzSound;

    private AudioSource audioSource;
    private bool isRecognizing = false;
    private bool prevB;
    private Coroutine busyPanelCoroutine;

    public string recognizedSpeech;
    private string pendingProcessedSpeech; // Storinge processed speech waiting for confirmation from the user

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        }
        if (recordAgainButton != null)
        {
            recordAgainButton.onClick.AddListener(OnRecordAgainButtonClicked);
        }
    }

    void Update()
    {
        bool space = Input.GetKeyDown(KeyCode.Space);

        bool bNow = false;
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bNow);

        bool bDown = bNow && !prevB;

        if (space || bDown)
        {
            if (isRecognizing)
            {
                Debug.Log("Speech recognition already in progress...");
                PlaySound(buzzSound);
            }
            // Block new input while waiting for user confirmation
            else if (mcpPromptSender != null && mcpPromptSender.IsWaitingForConfirmation)
            {
                //Debug.Log("Please confirm or re-record the current speech first...");
                PlaySound(buzzSound);
            }
            // Block new input while MCP request is being proccesed
            else if (mcpPromptSender != null && mcpPromptSender.IsBusy)
            {
                Debug.Log("MCP busy, please wait for previous response...");
                ShowBusyBlocked();
            }
            else
            {
                Debug.Log("Starting speech recognition...");
                PlaySound(startSound);
                _ = RecognizeSpeechAsync();
            }
        }
        prevB = bNow;
    }


    /// <summary>
    /// The key is kept out of source control: Inspector field (if set locally) → AZURE_SPEECH_KEY env var → SpeechKey.local.txt.
    /// </summary>
    private string ResolveSpeechKey()
    {
        if (!string.IsNullOrWhiteSpace(speechKey))
            return speechKey.Trim();

        string fromEnv = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        string keyFile = System.IO.Path.Combine(Application.dataPath, "..", "SpeechKey.local.txt");
        if (System.IO.File.Exists(keyFile))
            return System.IO.File.ReadAllText(keyFile).Trim();

        return null;
    }

    private async Task RecognizeSpeechAsync()
    {
        isRecognizing = true;

        try
        {
            string key = ResolveSpeechKey();
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[Speech] No Azure Speech key found. Set the AZURE_SPEECH_KEY environment variable " +
                               "or put the key in SpeechKey.local.txt in the project root.");
                PlaySound(buzzSound);
                return;
            }

            var speechConfig = SpeechConfig.FromSubscription(key, speechRegion);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            Debug.Log($"[Speech] Config initialized. Region='{speechRegion}', Language='{speechConfig.SpeechRecognitionLanguage}'.");

            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            Debug.Log("[Speech] Using default microphone input device.");

            using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            Debug.Log("Listening...");
            var result = await speechRecognizer.RecognizeOnceAsync();

            Debug.Log($"[Speech] Recognition completed. Reason={result.Reason}, ResultId={result.ResultId}, Duration={result.Duration}.");
            PlaySound(endSound);
            ProcessSpeechResult(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Speech] Exception during recognition: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            isRecognizing = false;
        }
    }

    private void ProcessSpeechResult(SpeechRecognitionResult result)
    {
        if (result == null)
        {
            Debug.LogError("[Speech] Result is null.");
            return;
        }

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            recognizedSpeech = result.Text;
            Debug.Log($"[Speech] Recognized: '{recognizedSpeech}'");

            string processedSpeech = ReplacePronounsWithObjectName(recognizedSpeech);

            // Store the processed speech for confirmation
            pendingProcessedSpeech = processedSpeech;

            if (mcpPromptSender != null)
            {
                // Showing captured input but doesn't send it yet, waits for confirmation
                mcpPromptSender.ShowUserPromptForConfirmation(processedSpeech);
            }
            else
            {
                Debug.LogError("MCP Prompt Sender script is NOT assigned!");
            }
        }
        else if (result.Reason == ResultReason.NoMatch)
        {
            var noMatch = NoMatchDetails.FromResult(result);
            Debug.LogWarning($"[Speech] NoMatch. Reason={noMatch.Reason}, Json={result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult)}");
            Debug.Log("Speech not recognized.");
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancel = CancellationDetails.FromResult(result);
            Debug.LogError($"[Speech] Canceled. CancellationReason={cancel.Reason}, ErrorCode={cancel.ErrorCode}, ErrorDetails={cancel.ErrorDetails}");
            Debug.LogError($"[Speech] Cancellation JSON: {result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult)}");
            Debug.Log("Speech not recognized.");
        }
        else
        {
            Debug.LogWarning($"[Speech] Unhandled recognition reason: {result.Reason}. Text='{result.Text}'");
            Debug.Log("Speech not recognized.");
        }
    }

    private void OnConfirmButtonClicked()
    {
        //Debug.Log("User confirmed the recognized text");
        PlaySound(confirmSound);

        if (mcpPromptSender != null)
        {
            // Send the confirmed speech to MCP
            mcpPromptSender.testPrompt = pendingProcessedSpeech;
            mcpPromptSender.SendConfirmedPrompt(pendingProcessedSpeech);
        }
    }

    private void OnRecordAgainButtonClicked()
    {
        //Debug.Log("User cancelled the recognized text");
        PlaySound(cancelSound);

        if (mcpPromptSender != null)
        {
            mcpPromptSender.HideUserPrompt();
        }

        Debug.Log("Ready for new recording. Press Space or B button to start.");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void ShowBusyBlocked()
    {
        PlaySound(buzzSound);
        if (busyPanel == null)
            return;

        if (busyPanelCoroutine != null)
            StopCoroutine(busyPanelCoroutine);

        busyPanelCoroutine = StartCoroutine(ShowBusyPanel());
    }

    private IEnumerator ShowBusyPanel()
    {
        busyPanel.SetActive(true);
        yield return new WaitForSeconds(busyShowSeconds);
        busyPanel.SetActive(false);
        busyPanelCoroutine = null;
    }

    private string ReplacePronounsWithObjectName(string speech)
    {
        if (automaticDescriptor == null || automaticDescriptor.lastLokkedObject == null)
        {
            return speech;
        }

        string objectName = automaticDescriptor.lastLokkedObject.name;
        string modified = speech;

        // Replace "this" and "that" with object name if looking at something

        if (modified.ToLower().Contains("this"))
        {
            modified = System.Text.RegularExpressions.Regex.Replace(modified, @"\bthis\b", objectName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Debug.Log($"Replaced 'this' with '{objectName}': {modified}");
        }
        if (modified.ToLower().Contains("that"))
        {
            modified = System.Text.RegularExpressions.Regex.Replace(modified, @"\bthat\b", objectName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Debug.Log($"Replaced 'that' with '{objectName}': {modified}");
        }

        return modified;
    }
}
