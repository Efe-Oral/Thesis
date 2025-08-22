using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstanceIdTest : MonoBehaviour
{

    [SerializeField] private McpPromptSender mcpPromptSender;

    void Start()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        int id = cube.GetInstanceID();
        string idString = id.ToString();

        Debug.Log("InstanceID int: " + id);
        Debug.Log("InstanceID string: \"" + idString + "\"");

        // Example prompt to simulate
        string prompt = "Delete object with id" + idString;
        Debug.Log("Prompt: " + prompt);

        if (mcpPromptSender != null)
        {
            mcpPromptSender.testPrompt = prompt;
            mcpPromptSender.Send(prompt);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
