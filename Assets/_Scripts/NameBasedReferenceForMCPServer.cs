using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NameBasedReferenceForMCPServer : MonoBehaviour
{
    [SerializeField] private McpPromptSender mcpPromptSender;
    [SerializeField] private GameObject targetObject;

    void Start()
    {
        if (targetObject != null)
        {
            // Use the object's name instead of instance ID
            string prompt = $"Delete the gameobject named \"{targetObject.name}\"";

            Debug.Log($"Target Object: {targetObject.name}");
            Debug.Log($"Generated Prompt: {prompt}");

            if (mcpPromptSender != null)
            {
                mcpPromptSender.testPrompt = prompt;
                mcpPromptSender.Send(prompt);
            }
        }
        else
        {
            Debug.LogError("Please assign a target object in the Inspector!");
        }
    }
}