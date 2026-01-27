using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using Framework.Utils.Editor;
#endif

public class PlayModeManuelSaver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform leftControllerTransform;

    [Header("Save UI Feedback")]
    [SerializeField] private GameObject saveEverythingMessage;
    [SerializeField] private GameObject saveIndividualMessage;
    [SerializeField] private AudioClip saveSound;
    [SerializeField] private float showSeconds = 1.5f;
    private AudioSource audioSource;

    [Header("Ray Settings")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private LayerMask raycastLayers = -1; // All layers
    [SerializeField] private bool showDebugRay = true;
    [SerializeField] private Color rayColor = Color.blue;

    [Header("Haptic Feedback Settings")]
    [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.5f;
    [SerializeField, Range(0f, 1f)] private float hapticDuration = 0.25f;

    private bool wasYButtonPressed = false;
    private bool wasXButtonPressed = false;
    private InputDevice leftHandDevice;
    private LineRenderer lineRenderer;

    private void Start()
    {
        GetLeftController();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && saveSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (showDebugRay)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = rayColor;
            lineRenderer.endColor = rayColor;
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.positionCount = 2;
        }
    }

    private void GetLeftController()
    {
        var leftHandDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftHandDevices);
        if (leftHandDevices.Count > 0)
        {
            leftHandDevice = leftHandDevices[0];
        }
    }

    private void Update()
    {
        if (!leftHandDevice.isValid)
        {
            GetLeftController();
            return;
        }

        if (leftControllerTransform == null)
        {
            Debug.LogWarning("Left controller transform not assigned!");
            return;
        }

        Ray ray = new Ray(leftControllerTransform.position, leftControllerTransform.forward);

        // Update debug ray visualization
        if (showDebugRay && lineRenderer != null)
        {
            lineRenderer.SetPosition(0, ray.origin);
            lineRenderer.SetPosition(1, ray.origin + ray.direction * rayDistance);
        }

        // Check Y button - for Save Everything
        bool yButtonValue;
        if (leftHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out yButtonValue))
        {
            // Check for button press (not held)
            if (yButtonValue && !wasYButtonPressed)
            {
                SaveEverything();
            }
            wasYButtonPressed = yButtonValue;
        }

        // Check X button - Save Individual Object
        bool xButtonValue;
        if (leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out xButtonValue))
        {
            if (xButtonValue && !wasXButtonPressed)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, rayDistance, raycastLayers))
                {
                    SaveIndividualObject(hit.collider.gameObject);
                }
            }
            wasXButtonPressed = xButtonValue;
        }
    }

    private void SaveEverything()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;

        // Calling the public SaveAllNewObjects method directly
        PlayModeSaveShortcut.SaveAllNewObjects();

        SendHapticFeedback(hapticAmplitude, hapticDuration);
        StartCoroutine(ShowSaveMessage(saveEverythingMessage));
        Debug.Log("Save Everything triggered");
#endif
    }

    private void SaveIndividualObject(GameObject hitObject)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;

        PlayModeSaveShortcut.SaveObjectFromRuntimeStatic(hitObject);
        SendHapticFeedback(hapticAmplitude, hapticDuration);  // Haptic for manual saving gameobj
        StartCoroutine(ShowSaveMessage(saveIndividualMessage));
        Debug.Log($"Saved individual object: {hitObject.name}");
#endif
    }

    private IEnumerator ShowSaveMessage(GameObject messageUI)
    {
        if (messageUI != null)
        {
            messageUI.SetActive(true);

            if (audioSource != null && saveSound != null)
            {
                audioSource.PlayOneShot(saveSound);
            }

            yield return new WaitForSeconds(showSeconds);
            messageUI.SetActive(false);
        }
    }

    private void SendHapticFeedback(float amplitude = 0.5f, float duration = 0.2f)
    {
        if (!leftHandDevice.isValid)
        {
            GetLeftController();
        }

        if (leftHandDevice.isValid)
        {
            leftHandDevice.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
