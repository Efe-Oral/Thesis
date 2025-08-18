using System;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

public class SpeechRecognition : MonoBehaviour
{
    [Header("Azure Speech Settings")]
    [SerializeField] private string speechKey = "DuTF9airVdsZZpxpgaQBj0TgJbQtkxGW22Cwrb014SyboVhXoziOJQQJ99BCACPV0roXJ3w3AAAYACOGBSBH";
    [SerializeField] private string speechRegion = "germanywestcentral"; // e.g. "germanywestcentral"

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip endSound;
    [SerializeField] private AudioClip buzzSound;

    private AudioSource audioSource;
    private bool isRecognizing = false;

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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isRecognizing)
            {
                Debug.Log("Speech recognition already in progress...");
                PlaySound(buzzSound);  // Play buzz while recognizing
            }
            else
            {
                Debug.Log("Starting speech recognition...");
                PlaySound(startSound);
                _ = RecognizeSpeechAsync();
            }
        }
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
