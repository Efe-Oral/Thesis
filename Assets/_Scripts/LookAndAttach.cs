using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAndAttach : MonoBehaviour
{

    [SerializeField] private AutomaticDescriptor automaticDescriptor;

    void Update()
    {
        AttachScript();
    }

    private void AttachScript()
    {
        GameObject target = automaticDescriptor.lastHitObject;

        if (target == null) return;

        // Check if Outline script is already attached
        if (target.GetComponent<Outline>() != null)
        {
            Debug.Log("Outline script already exists");
            return;
        }

        // Attach Outline script
        Debug.Log("Adding Outline script");
        target.AddComponent<Outline>();
    }
}