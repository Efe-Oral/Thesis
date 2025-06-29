using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor.Build.Content;


public class MCPVoiceCommandTool : MonoBehaviour
{

    [Tooltip("The path to the .txt file where the command will be saved.")]
    [SerializeField] private string filePath = @"C:/Users/efeor/OneDrive/Masaüstü/mcp_voice_command.txt";

    [Tooltip("Manuel test command")]
    [SerializeField] private string testCommand;

    private SpeechRecognition speechRecognition;

    void Start()
    {
        speechRecognition = FindObjectOfType<SpeechRecognition>();
    }

    void Update()
    {
        if (speechRecognition.recognizedSpeech != null)
        {
            WriteCommandToFile(speechRecognition.recognizedSpeech);
            speechRecognition.recognizedSpeech = null;
        }

        //Find a way to press enter in inspector mode
        /*if (testCommand != null && Input.GetKeyDown(KeyCode.Return))
        {
            WriteCommandToFile(testCommand);
            testCommand = null;
        }*/

    }

    public void WriteCommandToFile(string command)
    {
        try
        {
            File.WriteAllText(filePath, command);
        }
        catch (IOException e)
        {
            Debug.Log($"Failed to write command to file: {e.Message}");
        }
    }
}
