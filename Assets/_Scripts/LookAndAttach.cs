using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAndAttach : MonoBehaviour
{

    private AutomaticDescriptor automaticDescriptor;

    private GameObject currentlyHighlightedObject;
    private Outline currentOutline;


    void Start()
    {
        automaticDescriptor = FindObjectOfType<AutomaticDescriptor>();
    }
    void Update()
    {
        AttachOutlineScript();
        UpdateHighlight(automaticDescriptor.lastLokkedObject);
        //AttachRigidbody();
    }

    private void AttachOutlineScript()
    {
        GameObject target = automaticDescriptor.lastLokkedObject;

        if (target == null) return;

        // Check if Outline script is already attached
        if (target.GetComponent<Outline>() != null)
        {
            Debug.Log("Outline script already exists");
            return;
        }

        else
        {
            Debug.Log("Adding Outline script");
            target.AddComponent<Outline>();
        }
    }

    private void UpdateHighlight(GameObject target)
    {
        // If we're still looking at the same object, do nothing

        if (target == currentlyHighlightedObject) /*3*/ // automaticDescriptor.lastLokkedObject == currentlyHighlightedObject
            return;

        // Disable outline on previously highlighted object

        if (currentOutline is not null)
        {
            currentOutline.enabled = false;
            currentlyHighlightedObject = null;
            currentOutline = null;
        }

        // If looking at a new valid object
        if (target != null)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.enabled = true;
            currentlyHighlightedObject = target;
            currentOutline = outline;
        }
    }

    private void AttachRigidbody()
    {
        if (automaticDescriptor.lastLokkedObject == null) return;

        if (automaticDescriptor.lastLokkedObject.GetComponent<Rigidbody>() != null)
        {
            Debug.Log("Egeeee Rigidbody component is already attached to " + automaticDescriptor.lastLokkedObject.name);
            return;
        }

        else
        {
            Debug.Log("Efeee Adding Ridigbody component to " + automaticDescriptor.lastLokkedObject.gameObject.name);
            automaticDescriptor.lastLokkedObject.AddComponent<Rigidbody>();
        }
    }

}