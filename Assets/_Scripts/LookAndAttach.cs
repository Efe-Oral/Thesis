using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAndAttach : MonoBehaviour
{

    [SerializeField] private AutomaticDescriptor automaticDescriptor;

    private GameObject currentlyHighlightedObject;
    private Outline currentOutline;


    void Update()
    {
        AttachScript();
        UpdateHighlight(automaticDescriptor.lastHitObject);
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

        else
        {
            Debug.Log("Adding Outline script");
            target.AddComponent<Outline>();
        }
    }

    private void UpdateHighlight(GameObject target)
    {
        // If we're still looking at the same object, do nothing

        if (target == currentlyHighlightedObject) /*3*/ // automaticDescriptor.lastHitObject == currentlyHighlightedObject
            return;

        // Disable outline on previously highlighted object

        if (currentOutline is not null)    /*2*/
        {
            currentOutline.enabled = false;
            currentlyHighlightedObject = null;
            currentOutline = null;
        }

        // If looking at a new valid object
        if (target != null)      /*1*/
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

}