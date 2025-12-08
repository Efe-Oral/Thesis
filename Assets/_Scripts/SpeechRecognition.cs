using System;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using UnityEngine.XR;
using UnityEngine.InputSystem;

public class SpeechRecognition : MonoBehaviour
{
    [Header("MCP Prompt Sender")]
    [SerializeField] private McpPromptSender mcpPromptSender;

    [Header("Azure Speech Settings")]
    [SerializeField] private string speechKey = "DuTF9airVdsZZpxpgaQBj0TgJbQtkxGW22Cwrb014SyboVhXoziOJQQJ99BCACPV0roXJ3w3AAAYACOGBSBH";
    [SerializeField] private string speechRegion = "germanywestcentral"; // e.g. "germanywestcentral"

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip endSound;
    [SerializeField] private AudioClip buzzSound;

    private AudioSource audioSource;
    private bool isRecognizing = false;

    private bool prevB;

    public string recognizedSpeech;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
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
            else
            {
                Debug.Log("Starting speech recognition...");
                PlaySound(startSound);
                _ = RecognizeSpeechAsync();
            }
        }
        prevB = bNow;
    }


    private async Task RecognizeSpeechAsync()
    {
        isRecognizing = true;

        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        Debug.Log("Listening...");
        var result = await speechRecognizer.RecognizeOnceAsync();

        PlaySound(endSound);
        ProcessSpeechResult(result);

        isRecognizing = false;
    }

    private void ProcessSpeechResult(SpeechRecognitionResult result)
    {
        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            recognizedSpeech = result.Text;
            Debug.Log("Recognized: " + recognizedSpeech);
            if (mcpPromptSender != null)
            {
                // Show user prompt on screen
                mcpPromptSender.ShowUserPrompt(recognizedSpeech);

                // Sending recognized speech to MCP bridge
                mcpPromptSender.testPrompt = recognizedSpeech;
                mcpPromptSender.Send(recognizedSpeech);

            }
            else
            {
                Debug.LogError("MCP Prompt Sender script is NOT assigned!");
            }
        }
        else
        {
            Debug.Log("Speech not recognized.");
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
