using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FloatingUI : MonoBehaviour
{
    [SerializeField] private GameObject startPoint;
    [SerializeField] private GameObject endPoint;

    private TextMeshPro objectName;

    AutomaticDescriptor automaticDescriptor;

    void Start()
    {
        automaticDescriptor = FindObjectOfType<AutomaticDescriptor>();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
